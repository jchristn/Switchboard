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
        private SwitchboardSettings _Settings = null;
        private LoggingModule _Logging = null;
        private Serializer _Serializer = null;
        private Random _Random = new Random(Guid.NewGuid().GetHashCode());
        private bool _IsDisposed = false;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private readonly object _TasksLock = new object();
        private Dictionary<string, CancellationTokenSource> _OriginTokens = new Dictionary<string, CancellationTokenSource>();

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

            foreach (OriginServer origin in _Settings.Origins)
            {
                StartOriginTask(origin);
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Reconcile the running health-check tasks against the current set of origin servers in
        /// settings. Starts tasks for newly added origins and cancels tasks for origins that have
        /// been removed. Existing origins are left running so their health state is preserved.
        /// Thread-safe.
        /// </summary>
        public void SyncOrigins()
        {
            if (_IsDisposed) return;

            lock (_TasksLock)
            {
                HashSet<string> current = new HashSet<string>();
                if (_Settings.Origins != null)
                {
                    foreach (OriginServer origin in _Settings.Origins)
                        current.Add(origin.Identifier);
                }

                // Cancel and remove tasks for origins that no longer exist.
                foreach (string identifier in new List<string>(_OriginTokens.Keys))
                {
                    if (!current.Contains(identifier))
                    {
                        try { _OriginTokens[identifier].Cancel(); } catch { }
                        _OriginTokens[identifier].Dispose();
                        _OriginTokens.Remove(identifier);
                        _Logging?.Debug(_Header + "stopped health checks for removed origin " + identifier);
                    }
                }

                // Start tasks for origins that are new.
                if (_Settings.Origins != null)
                {
                    foreach (OriginServer origin in _Settings.Origins)
                    {
                        if (!_OriginTokens.ContainsKey(origin.Identifier))
                        {
                            StartOriginTask(origin);
                            _Logging?.Debug(_Header + "started health checks for new origin " + origin.Identifier);
                        }
                    }
                }
            }
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

                    lock (_TasksLock)
                    {
                        foreach (CancellationTokenSource cts in _OriginTokens.Values)
                        {
                            try { cts.Dispose(); } catch { }
                        }
                        _OriginTokens.Clear();
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

        private void StartOriginTask(OriginServer origin)
        {
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(_TokenSource.Token);
            _OriginTokens[origin.Identifier] = cts;
            _ = Task.Run(() => HealthCheckTask(origin, cts.Token), cts.Token);
        }

        private async Task HealthCheckTask(OriginServer origin, CancellationToken token = default)
        {
            _Logging.Debug(
                _Header +
                "starting healthcheck task for origin " +
                origin.Identifier + " " + origin.Name + " " + origin.Hostname + ":" + origin.Port);

            string healthCheckUrl = (origin.Ssl ? "https://" : "http://") + origin.Hostname + ":" + origin.Port + origin.HealthCheckUrl;
            string previousTarget = null;

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);

                while (!token.IsCancellationRequested)
                {
                    // Recompute the target each iteration so edits to the origin's hostname, port, SSL, health
                    // check URL, or method (applied in place by the configuration reload without restarting
                    // this task) are picked up live. When the target actually changes, reset the health
                    // telemetry so metrics and history describe the new target rather than the old one.
                    healthCheckUrl = (origin.Ssl ? "https://" : "http://") + origin.Hostname + ":" + origin.Port + origin.HealthCheckUrl;
                    string target = origin.HealthCheckMethod + " " + healthCheckUrl;
                    if (previousTarget != null && !String.Equals(previousTarget, target, StringComparison.Ordinal))
                    {
                        ResetHealthState(origin);
                        _Logging.Info(_Header + "origin " + origin.Identifier + " health target changed to " + target + "; resetting health state");
                    }
                    previousTarget = target;

                    try
                    {
                        HttpRequestMessage request = new HttpRequestMessage(HttpMethodConverter(origin.HealthCheckMethod), healthCheckUrl);
                        HttpResponseMessage response = await client.SendAsync(request, token);

                        if (response.IsSuccessStatusCode)
                        {
                            RecordSuccess(origin);
                            _Logging.Debug(_Header + "health check succeeded for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl);
                        }
                        else
                        {
                            RecordFailure(origin, "HTTP " + (int)response.StatusCode);
                            _Logging.Debug(_Header + "health check failed for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl + " with status " + (int)response.StatusCode);
                        }
                    }
                    catch (HttpRequestException hre)
                    {
                        RecordFailure(origin, hre.Message);
                        _Logging.Debug(_Header + "health check failed for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl + ": " + hre.Message);
                    }
                    catch (HttpIOException ioe)
                    {
                        RecordFailure(origin, ioe.Message);
                        _Logging.Debug(_Header + "health check failed for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl + ": " + ioe.Message);
                    }
                    catch (SocketException se)
                    {
                        RecordFailure(origin, se.Message);
                        _Logging.Debug(_Header + "health check failed for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl + ": " + se.Message);
                    }
                    catch (TaskCanceledException)
                    {
                        // Expected when cancellation is requested or a timeout occurs. Only a timeout is a failure.
                        if (!token.IsCancellationRequested)
                        {
                            RecordFailure(origin, "Timeout");
                            _Logging.Debug(_Header + "health check timeout for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested or a timeout occurs. Only a timeout is a failure.
                        if (!token.IsCancellationRequested)
                        {
                            RecordFailure(origin, "Timeout");
                            _Logging.Debug(_Header + "health check timeout for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl);
                        }
                    }
                    catch (Exception e)
                    {
                        RecordFailure(origin, e.Message);
                        _Logging.Debug(_Header + "health check exception for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl + Environment.NewLine + e.ToString());
                    }

                    // Wait before next health check
                    if (!token.IsCancellationRequested)
                    {
                        await Task.Delay(origin.HealthCheckIntervalMs, token);
                    }
                }
            }

            _Logging.Debug(_Header + "stopping healthcheck task for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl);
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

        // Reset all runtime health telemetry for an origin, used when its health-check target changes so
        // metrics and history describe the new target from a clean slate. Marks the origin unhealthy until
        // it re-proves itself against the new address.
        private void ResetHealthState(OriginServer origin)
        {
            lock (origin.Lock)
            {
                origin.Healthy = false;
                origin.HealthCheckSuccess = 0;
                origin.HealthCheckFailure = 0;
                origin.LastError = null;
                origin.FirstCheckUtc = null;
                origin.LastCheckUtc = null;
                origin.LastHealthyUtc = null;
                origin.LastUnhealthyUtc = null;
                origin.LastStateChangeUtc = null;
                origin.TotalUptimeMs = 0;
                origin.TotalDowntimeMs = 0;
                origin.CheckHistory.Clear();
            }
        }

        // Append a check result and prune records older than the 24-hour retention window.
        // Caller must hold origin.Lock.
        private void AppendHistory(OriginServer origin, DateTime now, bool success)
        {
            origin.CheckHistory.Add(new HealthCheckRecord(now, success));
            DateTime cutoff = now - _HistoryRetention;
            origin.CheckHistory.RemoveAll(r => r.TimestampUtc < cutoff);
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

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
