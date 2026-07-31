namespace Switchboard.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using RestWrapper;
    using SerializationHelper;
    using Switchboard.Core.Models;
    using SyslogLogging;
    using Timestamps;
    using UrlMatcher;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Health check service.
    /// </summary>
    public class HealthCheckService : IDisposable
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        /// <summary>
        /// Logging module.
        /// </summary>
        public LoggingModule Logging
        {
            get => _Logging;
            set => _Logging = value ?? throw new ArgumentNullException(nameof(Logging));
        }

        #endregion

        #region Private-Members

        private readonly string _Header = "[HealthCheckService] ";
        private static readonly TimeSpan _HistoryRetention = TimeSpan.FromHours(24);
        private const int _MaxHistorySamples = 200;
        private SwitchboardSettings _Settings = null;
        private LoggingModule _Logging = null;
        private Serializer _Serializer = null;
        private Random _Random = new Random(Guid.NewGuid().GetHashCode());
        private bool _IsDisposed = false;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private readonly object _MonitorsLock = new object();
        // One monitor per unique health-check target (method + scheme + host + port + URL). Every origin
        // that resolves to the same target subscribes to that single monitor, so the target is probed
        // once and the result is applied to all subscribing origins. Keyed by the target signature.
        private readonly Dictionary<string, OriginMonitor> _Monitors = new Dictionary<string, OriginMonitor>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Health check service.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="logging">Logging.</param>
        /// <param name="serializer">Serializer.</param>
        public HealthCheckService(
            SwitchboardSettings settings,
            LoggingModule logging,
            Serializer serializer)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            Reconcile();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Reconcile the running health monitors against the current set of origin servers in settings.
        /// Origins are grouped by their health-check target so each unique target is probed once and the
        /// result is applied to every origin sharing it. Starts monitors for newly seen targets, stops
        /// monitors whose targets are gone, and re-subscribes origins whose target changed. Existing
        /// monitors keep running so their health state is preserved. Thread-safe.
        /// </summary>
        public void SyncOrigins()
        {
            Reconcile();
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_IsDisposed)
            {
                if (disposing)
                {
                    try { _TokenSource.Cancel(); } catch { }

                    lock (_MonitorsLock)
                    {
                        foreach (OriginMonitor monitor in _Monitors.Values)
                        {
                            try { monitor.Cts.Cancel(); } catch { }
                            try { monitor.Cts.Dispose(); } catch { }
                        }
                        _Monitors.Clear();
                    }

                    _TokenSource.Dispose();
                    _Random = null;
                    _Serializer = null;
                    _Logging = null;
                    _Settings = null;
                }

                _IsDisposed = true;
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        // Group origins by health-check target and reconcile the running monitors: start monitors for
        // new targets, stop monitors whose target no longer has any origin, and update the subscriber
        // list of existing monitors. When an origin joins an existing monitor it inherits that monitor's
        // current health snapshot so all origins sharing a target stay consistent. Thread-safe.
        private void Reconcile()
        {
            if (_IsDisposed) return;

            lock (_MonitorsLock)
            {
                Dictionary<string, List<OriginServer>> desired = new Dictionary<string, List<OriginServer>>();
                if (_Settings?.Origins != null)
                {
                    foreach (OriginServer origin in _Settings.Origins)
                    {
                        if (origin == null || String.IsNullOrEmpty(origin.Hostname)) continue;

                        string key = MonitorKey(origin);
                        if (!desired.TryGetValue(key, out List<OriginServer> list))
                        {
                            list = new List<OriginServer>();
                            desired[key] = list;
                        }
                        list.Add(origin);
                    }
                }

                // Stop monitors whose target no longer has any origin.
                foreach (string key in new List<string>(_Monitors.Keys))
                {
                    if (!desired.ContainsKey(key))
                    {
                        OriginMonitor stale = _Monitors[key];
                        try { stale.Cts.Cancel(); } catch { }
                        try { stale.Cts.Dispose(); } catch { }
                        _Monitors.Remove(key);
                        _Logging?.Debug(_Header + "stopped health monitor for " + key);
                    }
                }

                // Start new monitors and update the subscriber list of existing ones.
                foreach (KeyValuePair<string, List<OriginServer>> kvp in desired)
                {
                    if (_Monitors.TryGetValue(kvp.Key, out OriginMonitor existing))
                    {
                        lock (existing.OriginsLock)
                        {
                            OriginServer reference = existing.Origins.Count > 0 ? existing.Origins[0] : null;

                            // An origin newly joining an existing target inherits the current health snapshot
                            // so its badge and history line up with its peers immediately rather than starting
                            // from an empty window.
                            if (reference != null)
                            {
                                foreach (OriginServer origin in kvp.Value)
                                {
                                    if (!existing.Origins.Contains(origin)) CopyHealthState(reference, origin);
                                }
                            }

                            existing.Origins = new List<OriginServer>(kvp.Value);
                        }
                    }
                    else
                    {
                        OriginMonitor monitor = new OriginMonitor
                        {
                            Origins = new List<OriginServer>(kvp.Value),
                            Cts = CancellationTokenSource.CreateLinkedTokenSource(_TokenSource.Token)
                        };
                        _Monitors[kvp.Key] = monitor;

                        string keyCopy = kvp.Key;
                        OriginMonitor monitorCopy = monitor;
                        CancellationToken token = monitor.Cts.Token;
                        _ = Task.Run(() => MonitorLoop(keyCopy, monitorCopy, token), token);
                        _Logging?.Debug(_Header + "started health monitor for " + keyCopy + " (" + kvp.Value.Count + " origin(s))");
                    }
                }
            }
        }

        // Probe a single health-check target on an interval and apply the result to every origin that
        // currently subscribes to it. The subscriber list is re-read each iteration so reconciliation
        // (origins added, removed, or edited onto/off this target) takes effect without restarting the
        // monitor. The probe interval is the smallest interval among the current subscribers.
        private async Task MonitorLoop(string key, OriginMonitor monitor, CancellationToken token)
        {
            _Logging.Debug(_Header + "starting health monitor for " + key);

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);

                while (!token.IsCancellationRequested)
                {
                    OriginServer[] subscribers;
                    lock (monitor.OriginsLock) subscribers = monitor.Origins.ToArray();

                    if (subscribers.Length == 0)
                    {
                        try { await Task.Delay(1000, token); } catch (OperationCanceledException) { break; }
                        continue;
                    }

                    OriginServer probe = subscribers[0];
                    string url = (probe.Ssl ? "https://" : "http://") + probe.Hostname + ":" + probe.Port + probe.HealthCheckUrl;

                    bool success = false;
                    bool cancelled = false;
                    string error = null;

                    try
                    {
                        HttpRequestMessage request = new HttpRequestMessage(HttpMethodConverter(probe.HealthCheckMethod), url);
                        HttpResponseMessage response = await client.SendAsync(request, token);
                        success = response.IsSuccessStatusCode;
                        if (!success) error = "HTTP " + (int)response.StatusCode;
                    }
                    catch (HttpRequestException hre) { error = hre.Message; }
                    catch (HttpIOException ioe) { error = ioe.Message; }
                    catch (SocketException se) { error = se.Message; }
                    catch (TaskCanceledException) { if (token.IsCancellationRequested) cancelled = true; else error = "Timeout"; }
                    catch (OperationCanceledException) { if (token.IsCancellationRequested) cancelled = true; else error = "Timeout"; }
                    catch (Exception e) { error = e.Message; }

                    if (cancelled) break;

                    // Fan the single probe result out to every origin subscribing to this target.
                    foreach (OriginServer origin in subscribers)
                    {
                        if (success) RecordSuccess(origin);
                        else RecordFailure(origin, error);
                    }

                    if (success)
                        _Logging.Debug(_Header + "health check succeeded for " + url + " (" + subscribers.Length + " origin(s))");
                    else
                        _Logging.Debug(_Header + "health check failed for " + url + " (" + subscribers.Length + " origin(s)): " + error);

                    int interval = Int32.MaxValue;
                    foreach (OriginServer origin in subscribers)
                        if (origin.HealthCheckIntervalMs < interval) interval = origin.HealthCheckIntervalMs;
                    if (interval == Int32.MaxValue) interval = 5000;

                    try { await Task.Delay(interval, token); } catch (OperationCanceledException) { break; }
                }
            }

            _Logging.Debug(_Header + "stopping health monitor for " + key);
        }

        // Build the target signature for an origin's health check: method, scheme, host, port, and URL.
        // Origins that produce the same signature are probed by a single shared monitor.
        private static string MonitorKey(OriginServer origin)
        {
            string scheme = origin.Ssl ? "https" : "http";
            string host = (origin.Hostname ?? String.Empty).Trim().ToLowerInvariant();
            string path = origin.HealthCheckUrl ?? "/";
            return origin.HealthCheckMethod + "|" + scheme + "://" + host + ":" + origin.Port + path;
        }

        // Copy the runtime health snapshot (state, counters, timestamps, and rolling history) from one
        // origin to another so an origin joining an existing target is immediately consistent with its peers.
        private static void CopyHealthState(OriginServer from, OriginServer to)
        {
            if (ReferenceEquals(from, to)) return;

            lock (from.Lock)
            {
                List<HealthCheckRecord> historyCopy = new List<HealthCheckRecord>(from.CheckHistory.Count);
                foreach (HealthCheckRecord record in from.CheckHistory)
                    historyCopy.Add(new HealthCheckRecord(record.TimestampUtc, record.Success));

                lock (to.Lock)
                {
                    to.Healthy = from.Healthy;
                    to.HealthCheckSuccess = from.HealthCheckSuccess;
                    to.HealthCheckFailure = from.HealthCheckFailure;
                    to.LastError = from.LastError;
                    to.FirstCheckUtc = from.FirstCheckUtc;
                    to.LastCheckUtc = from.LastCheckUtc;
                    to.LastHealthyUtc = from.LastHealthyUtc;
                    to.LastUnhealthyUtc = from.LastUnhealthyUtc;
                    to.LastStateChangeUtc = from.LastStateChangeUtc;
                    to.TotalUptimeMs = from.TotalUptimeMs;
                    to.TotalDowntimeMs = from.TotalDowntimeMs;
                    to.CheckHistory.Clear();
                    to.CheckHistory.AddRange(historyCopy);
                }
            }
        }

        // Record a successful health check against the origin and flip it healthy once the healthy
        // threshold is reached. Updates the rolling history, timestamps, and banked uptime/downtime
        // under the origin's lock.
        private void RecordSuccess(OriginServer origin)
        {
            DateTime now = DateTime.UtcNow;

            lock (origin.Lock)
            {
                if (origin.FirstCheckUtc == null)
                {
                    origin.FirstCheckUtc = now;
                    origin.LastStateChangeUtc = now;
                }

                origin.LastCheckUtc = now;
                AppendHistory(origin, now, true);

                if (origin.HealthCheckSuccess < 99) origin.HealthCheckSuccess++;
                origin.HealthCheckFailure = 0;
                origin.LastError = null;

                if (!origin.Healthy && origin.HealthCheckSuccess >= origin.HealthyThreshold)
                {
                    if (origin.LastStateChangeUtc.HasValue)
                    {
                        long downtimeMs = (long)(now - origin.LastStateChangeUtc.Value).TotalMilliseconds;
                        if (downtimeMs > 0) origin.TotalDowntimeMs += downtimeMs;
                    }

                    origin.Healthy = true;
                    origin.LastHealthyUtc = now;
                    origin.LastStateChangeUtc = now;
                    _Logging.Info(_Header + "origin " + origin.Identifier + " is now healthy");
                }
            }
        }

        // Record a failed health check against the origin and flip it unhealthy once the unhealthy
        // threshold is reached. Captures the error message and banks uptime on transition, under lock.
        private void RecordFailure(OriginServer origin, string errorMessage)
        {
            DateTime now = DateTime.UtcNow;

            lock (origin.Lock)
            {
                if (origin.FirstCheckUtc == null)
                {
                    origin.FirstCheckUtc = now;
                    origin.LastStateChangeUtc = now;
                }

                origin.LastCheckUtc = now;
                AppendHistory(origin, now, false);

                if (origin.HealthCheckFailure < 99) origin.HealthCheckFailure++;
                origin.HealthCheckSuccess = 0;
                origin.LastError = errorMessage;

                if (origin.Healthy && origin.HealthCheckFailure >= origin.UnhealthyThreshold)
                {
                    if (origin.LastStateChangeUtc.HasValue)
                    {
                        long uptimeMs = (long)(now - origin.LastStateChangeUtc.Value).TotalMilliseconds;
                        if (uptimeMs > 0) origin.TotalUptimeMs += uptimeMs;
                    }

                    origin.Healthy = false;
                    origin.LastUnhealthyUtc = now;
                    origin.LastStateChangeUtc = now;
                    _Logging.Warn(_Header + "origin " + origin.Identifier + " is now unhealthy: " + (errorMessage ?? "check failed"));
                }
            }
        }

        // Append a check result to the rolling FIFO history. Prunes records older than the 24-hour
        // retention window, then bounds the window to the most recent _MaxHistorySamples entries so the
        // oldest samples fall off once the queue is full. Caller must hold origin.Lock.
        private void AppendHistory(OriginServer origin, DateTime now, bool success)
        {
            origin.CheckHistory.Add(new HealthCheckRecord(now, success));

            DateTime cutoff = now - _HistoryRetention;
            origin.CheckHistory.RemoveAll(r => r.TimestampUtc < cutoff);

            int overflow = origin.CheckHistory.Count - _MaxHistorySamples;
            if (overflow > 0) origin.CheckHistory.RemoveRange(0, overflow);
        }

        private System.Net.Http.HttpMethod HttpMethodConverter(WatsonWebserver.Core.HttpMethod method)
        {
            switch (method)
            {
                case WatsonWebserver.Core.HttpMethod.GET:
                    return System.Net.Http.HttpMethod.Get;
                case WatsonWebserver.Core.HttpMethod.HEAD:
                    return System.Net.Http.HttpMethod.Head;
                case WatsonWebserver.Core.HttpMethod.PUT:
                    return System.Net.Http.HttpMethod.Put;
                case WatsonWebserver.Core.HttpMethod.POST:
                    return System.Net.Http.HttpMethod.Post;
                case WatsonWebserver.Core.HttpMethod.DELETE:
                    return System.Net.Http.HttpMethod.Delete;
                case WatsonWebserver.Core.HttpMethod.PATCH:
                    return System.Net.Http.HttpMethod.Patch;
                case WatsonWebserver.Core.HttpMethod.OPTIONS:
                    return System.Net.Http.HttpMethod.Options;
                case WatsonWebserver.Core.HttpMethod.TRACE:
                    return System.Net.Http.HttpMethod.Trace;
                default:
                    throw new ArgumentException($"Unsupported HTTP method: {method}");
            }
        }

        // A single shared health-check monitor: the probe loop's cancellation source and the mutable set
        // of origins that subscribe to this monitor's target (guarded by OriginsLock).
        private sealed class OriginMonitor
        {
            internal CancellationTokenSource Cts;
            internal List<OriginServer> Origins = new List<OriginServer>();
            internal readonly object OriginsLock = new object();
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
