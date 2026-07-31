#nullable enable

namespace Switchboard.Core.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Globalization;
    using System.Threading;

    /// <summary>
    /// Process-wide OpenTelemetry instrument registry for Switchboard. Holds the shared
    /// <see cref="System.Diagnostics.Metrics.Meter"/> and <see cref="System.Diagnostics.ActivitySource"/>
    /// plus every metric instrument. Instruments are created once and are inert no-ops until a provider
    /// (built by <c>TelemetryService</c>) begins listening, so the hot path can record unconditionally.
    /// </summary>
    /// <remarks>
    /// Thread safety: instrument recording is thread-safe. Observable gauges snapshot the settings reference
    /// set by <see cref="SetSettings(Switchboard.Core.SwitchboardSettings)"/> at collection time; per-origin
    /// double state (EWMA latency) is read under the origin lock.
    /// </remarks>
    public static class SwitchboardTelemetry
    {
        #region Public-Members

        /// <summary>
        /// The name shared by the meter and activity source ("Switchboard").
        /// </summary>
        public const string SourceName = "Switchboard";

        /// <summary>
        /// The shared activity source used to create request/proxy spans.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new ActivitySource(SourceName, Constants.SoftwareVersion);

        /// <summary>
        /// The shared meter that owns all Switchboard metric instruments.
        /// </summary>
        public static readonly Meter Meter = new Meter(SourceName, Constants.SoftwareVersion);

        #endregion

        #region Private-Members

        private static readonly Counter<long> _RequestsTotal = Meter.CreateCounter<long>(
            "switchboard_requests_total", "requests", "Total requests handled by the gateway, by endpoint, method, and response code.");
        private static readonly Counter<long> _GatewayRejectionsTotal = Meter.CreateCounter<long>(
            "switchboard_gateway_rejections_total", "requests", "Requests rejected by the gateway before or instead of proxying, by reason (status code).");
        private static readonly Counter<long> _OriginRequestsTotal = Meter.CreateCounter<long>(
            "switchboard_origin_requests_total", "requests", "Requests proxied to origin servers, by origin and response code.");
        private static readonly Counter<long> _OriginHealthChecksTotal = Meter.CreateCounter<long>(
            "switchboard_origin_health_checks_total", "checks", "Active health-check probes, by origin and result (success/failure).");
        private static readonly Counter<long> _OriginEjectionsTotal = Meter.CreateCounter<long>(
            "switchboard_origin_ejections_total", "ejections", "Passive-health ejections of an origin from routing, by origin.");
        private static readonly Counter<long> _LbSelectionsTotal = Meter.CreateCounter<long>(
            "switchboard_lb_selections_total", "selections", "Load-balancer origin selections, by endpoint and origin.");
        private static readonly Counter<long> _RetriesTotal = Meter.CreateCounter<long>(
            "switchboard_retries_total", "retries", "Additional proxy attempts beyond the first, by endpoint.");
        private static readonly Counter<long> _FailoversTotal = Meter.CreateCounter<long>(
            "switchboard_failovers_total", "failovers", "Failed attempts that fell over to another origin, by endpoint.");

        private static readonly Histogram<double> _RequestDurationSeconds = Meter.CreateHistogram<double>(
            "switchboard_request_duration_seconds", "s", "Proxied-request latency in seconds, by endpoint and origin.");
        private static readonly Histogram<long> _RequestBodyBytes = Meter.CreateHistogram<long>(
            "switchboard_request_body_bytes", "By", "Request body size in bytes, by endpoint.");
        private static readonly Histogram<long> _ResponseBodyBytes = Meter.CreateHistogram<long>(
            "switchboard_response_body_bytes", "By", "Response body size in bytes, by endpoint.");

        private static SwitchboardSettings? _Settings = null;

        #endregion

        #region Constructors-and-Factories

        static SwitchboardTelemetry()
        {
            Meter.CreateObservableGauge<long>("switchboard_build_info", ObserveBuildInfo, null, "Static build information; value is always 1, version carried as a label.");
            Meter.CreateObservableGauge<long>("switchboard_config_origins", ObserveConfigOrigins, "origins", "Number of configured origin servers.");
            Meter.CreateObservableGauge<long>("switchboard_config_endpoints", ObserveConfigEndpoints, "endpoints", "Number of configured API endpoints.");
            Meter.CreateObservableGauge<long>("switchboard_origin_up", ObserveOriginUp, null, "Origin health, 1 if healthy else 0, by origin.");
            Meter.CreateObservableGauge<long>("switchboard_origin_ejected", ObserveOriginEjected, null, "Origin ejection state, 1 if ejected from routing else 0, by origin.");
            Meter.CreateObservableGauge<long>("switchboard_origin_active_requests", ObserveOriginActive, "requests", "In-flight requests being served by the origin, by origin.");
            Meter.CreateObservableGauge<long>("switchboard_origin_pending_requests", ObserveOriginPending, "requests", "Requests queued for the origin, by origin.");
            Meter.CreateObservableGauge<double>("switchboard_origin_ewma_latency_seconds", ObserveOriginEwma, "s", "Exponentially-weighted moving average of origin latency in seconds, by origin.");
            Meter.CreateObservableGauge<double>("switchboard_origin_uptime_ratio", ObserveOriginUptime, null, "Fraction of monitored time the origin has been healthy (0.0-1.0), by origin.");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register the live settings whose origins/endpoints the observable gauges snapshot at collection time.
        /// </summary>
        /// <param name="settings">The active settings; may not be null.</param>
        public static void SetSettings(SwitchboardSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            Interlocked.Exchange(ref _Settings, settings);
        }

        /// <summary>
        /// Clear the settings reference so observable gauges report nothing. Called when telemetry is disposed.
        /// </summary>
        public static void ClearSettings()
        {
            Interlocked.Exchange(ref _Settings, null);
        }

        /// <summary>
        /// Record a completed gateway request.
        /// </summary>
        /// <param name="endpoint">Endpoint identifier, or a bounded placeholder such as "none".</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="statusCode">Final response status code.</param>
        public static void RecordRequest(string endpoint, string method, int statusCode)
        {
            _RequestsTotal.Add(1,
                new KeyValuePair<string, object?>("endpoint", endpoint),
                new KeyValuePair<string, object?>("method", method),
                new KeyValuePair<string, object?>("code", statusCode.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Record a gateway rejection (e.g., 400/401/413/429/502/505).
        /// </summary>
        /// <param name="statusCode">The rejection status code, used as the bounded "reason" label.</param>
        public static void RecordRejection(int statusCode)
        {
            _GatewayRejectionsTotal.Add(1,
                new KeyValuePair<string, object?>("reason", statusCode.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Record a proxied request to an origin.
        /// </summary>
        /// <param name="origin">Origin identifier.</param>
        /// <param name="statusCode">Response status code from the origin (0 if none).</param>
        public static void RecordOriginRequest(string origin, int statusCode)
        {
            _OriginRequestsTotal.Add(1,
                new KeyValuePair<string, object?>("origin", origin),
                new KeyValuePair<string, object?>("code", statusCode.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Record proxied-request latency.
        /// </summary>
        /// <param name="endpoint">Endpoint identifier.</param>
        /// <param name="origin">Origin identifier.</param>
        /// <param name="seconds">Latency in seconds.</param>
        public static void RecordDuration(string endpoint, string origin, double seconds)
        {
            _RequestDurationSeconds.Record(seconds,
                new KeyValuePair<string, object?>("endpoint", endpoint),
                new KeyValuePair<string, object?>("origin", origin));
        }

        /// <summary>
        /// Record request and response body sizes for an endpoint.
        /// </summary>
        /// <param name="endpoint">Endpoint identifier.</param>
        /// <param name="requestBytes">Request body size in bytes (negative values ignored).</param>
        /// <param name="responseBytes">Response body size in bytes (negative values ignored).</param>
        public static void RecordBodySizes(string endpoint, long requestBytes, long responseBytes)
        {
            KeyValuePair<string, object?> tag = new KeyValuePair<string, object?>("endpoint", endpoint);
            if (requestBytes >= 0) _RequestBodyBytes.Record(requestBytes, tag);
            if (responseBytes >= 0) _ResponseBodyBytes.Record(responseBytes, tag);
        }

        /// <summary>
        /// Record a health-check probe result.
        /// </summary>
        /// <param name="origin">Origin identifier.</param>
        /// <param name="success">True if the probe succeeded.</param>
        public static void RecordHealthCheck(string origin, bool success)
        {
            _OriginHealthChecksTotal.Add(1,
                new KeyValuePair<string, object?>("origin", origin),
                new KeyValuePair<string, object?>("result", success ? "success" : "failure"));
        }

        /// <summary>
        /// Record a passive-health ejection of an origin.
        /// </summary>
        /// <param name="origin">Origin identifier.</param>
        public static void RecordEjection(string origin)
        {
            _OriginEjectionsTotal.Add(1, new KeyValuePair<string, object?>("origin", origin));
        }

        /// <summary>
        /// Record a load-balancer origin selection.
        /// </summary>
        /// <param name="endpoint">Endpoint identifier.</param>
        /// <param name="origin">Origin identifier.</param>
        public static void RecordSelection(string endpoint, string origin)
        {
            _LbSelectionsTotal.Add(1,
                new KeyValuePair<string, object?>("endpoint", endpoint),
                new KeyValuePair<string, object?>("origin", origin));
        }

        /// <summary>
        /// Record an additional proxy attempt beyond the first for an endpoint.
        /// </summary>
        /// <param name="endpoint">Endpoint identifier.</param>
        public static void RecordRetry(string endpoint)
        {
            _RetriesTotal.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint));
        }

        /// <summary>
        /// Record a failover to another origin after a failed attempt.
        /// </summary>
        /// <param name="endpoint">Endpoint identifier.</param>
        public static void RecordFailover(string endpoint)
        {
            _FailoversTotal.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint));
        }

        #endregion

        #region Private-Methods

        private static IEnumerable<Measurement<long>> ObserveBuildInfo()
        {
            yield return new Measurement<long>(1, new KeyValuePair<string, object?>("version", Constants.SoftwareVersion));
        }

        private static IEnumerable<Measurement<long>> ObserveConfigOrigins()
        {
            SwitchboardSettings? settings = _Settings;
            if (settings == null) yield break;
            yield return new Measurement<long>(settings.Origins.Count);
        }

        private static IEnumerable<Measurement<long>> ObserveConfigEndpoints()
        {
            SwitchboardSettings? settings = _Settings;
            if (settings == null) yield break;
            yield return new Measurement<long>(settings.Endpoints.Count);
        }

        private static IEnumerable<Measurement<long>> ObserveOriginUp()
        {
            SwitchboardSettings? settings = _Settings;
            if (settings == null) yield break;
            foreach (OriginServer origin in settings.Origins)
            {
                yield return new Measurement<long>(origin.Healthy ? 1 : 0, OriginTag(origin));
            }
        }

        private static IEnumerable<Measurement<long>> ObserveOriginEjected()
        {
            SwitchboardSettings? settings = _Settings;
            if (settings == null) yield break;
            DateTime now = DateTime.UtcNow;
            foreach (OriginServer origin in settings.Origins)
            {
                bool ejected;
                lock (origin.Lock)
                {
                    ejected = origin.EjectedUntilUtc.HasValue && origin.EjectedUntilUtc.Value > now;
                }
                yield return new Measurement<long>(ejected ? 1 : 0, OriginTag(origin));
            }
        }

        private static IEnumerable<Measurement<long>> ObserveOriginActive()
        {
            SwitchboardSettings? settings = _Settings;
            if (settings == null) yield break;
            foreach (OriginServer origin in settings.Origins)
            {
                yield return new Measurement<long>(Volatile.Read(ref origin.ActiveRequests), OriginTag(origin));
            }
        }

        private static IEnumerable<Measurement<long>> ObserveOriginPending()
        {
            SwitchboardSettings? settings = _Settings;
            if (settings == null) yield break;
            foreach (OriginServer origin in settings.Origins)
            {
                yield return new Measurement<long>(Volatile.Read(ref origin.PendingRequests), OriginTag(origin));
            }
        }

        private static IEnumerable<Measurement<double>> ObserveOriginEwma()
        {
            SwitchboardSettings? settings = _Settings;
            if (settings == null) yield break;
            foreach (OriginServer origin in settings.Origins)
            {
                double ewmaMs;
                lock (origin.Lock)
                {
                    ewmaMs = origin.EwmaLatencyMs;
                }
                yield return new Measurement<double>(ewmaMs / 1000.0, OriginTag(origin));
            }
        }

        private static IEnumerable<Measurement<double>> ObserveOriginUptime()
        {
            SwitchboardSettings? settings = _Settings;
            if (settings == null) yield break;
            foreach (OriginServer origin in settings.Origins)
            {
                long up;
                long down;
                lock (origin.Lock)
                {
                    up = origin.TotalUptimeMs;
                    down = origin.TotalDowntimeMs;
                }
                long total = up + down;
                double ratio = total > 0 ? (double)up / total : 0.0;
                yield return new Measurement<double>(ratio, OriginTag(origin));
            }
        }

        private static KeyValuePair<string, object?> OriginTag(OriginServer origin)
        {
            return new KeyValuePair<string, object?>("origin", origin.Identifier ?? "unknown");
        }

        #endregion
    }
}
