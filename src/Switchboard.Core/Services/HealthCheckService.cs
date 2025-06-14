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

        #endregion

        #region Private-Members

        private readonly string _Header = "[HealthCheckService] ";
        private SwitchboardSettings _Settings = null;
        private LoggingModule _Logging = null;
        private Serializer _Serializer = null;
        private Random _Random = new Random(Guid.NewGuid().GetHashCode());
        private bool _IsDisposed = false;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private Dictionary<string, Task> _HealthCheckTasks = new Dictionary<string, Task>();

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
                _HealthCheckTasks.Add(
                    origin.Identifier,
                    Task.Run(() => HealthCheckTask(origin, _TokenSource.Token), _TokenSource.Token));
            }
        }

        #endregion

        #region Public-Methods

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

        private async Task HealthCheckTask(OriginServer origin, CancellationToken token = default)
        {
            bool firstRun = true;

            _Logging.Debug(
                _Header +
                "starting healthcheck task for origin " +
                origin.Identifier + " " + origin.Name + " " + origin.Hostname + ":" + origin.Port);

            string healthCheckUrl = (origin.Ssl ? "https://" : "http://") + origin.Hostname + ":" + origin.Port + origin.HealthCheckUrl;

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (!firstRun) await Task.Delay(origin.HealthCheckIntervalMs, token);
                        else firstRun = false;

                        HttpRequestMessage request = new HttpRequestMessage(HttpMethodConverter(origin.HealthCheckMethod), healthCheckUrl);
                        HttpResponseMessage response = await client.SendAsync(request, token);

                        if (response.IsSuccessStatusCode)
                        {
                            lock (origin.Lock)
                            {
                                if (origin.HealthCheckSuccess < 99) origin.HealthCheckSuccess++;
                                origin.HealthCheckFailure = 0;

                                if (!origin.Healthy && origin.HealthCheckSuccess >= origin.HealthyThreshold)
                                {
                                    origin.Healthy = true;
                                    _Logging.Info(_Header + "origin " + origin.Identifier + " is now healthy");
                                }
                            }

                            _Logging.Debug(_Header + "health check succeeded for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl);
                        }
                        else
                        {
                            lock (origin.Lock)
                            {
                                if (origin.HealthCheckFailure < 99) origin.HealthCheckFailure++;
                                origin.HealthCheckSuccess = 0;

                                if (origin.Healthy && origin.HealthCheckFailure >= origin.UnhealthyThreshold)
                                {
                                    origin.Healthy = false;
                                    _Logging.Warn(_Header + "origin " + origin.Identifier + " is now unhealthy (HTTP " + (int)response.StatusCode + ")");
                                }
                            }

                            _Logging.Debug(_Header + "health check failed for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl + " with status " + (int)response.StatusCode);
                        }
                    }
                    catch (HttpRequestException hre)
                    {
                        lock (origin.Lock)
                        {
                            if (origin.HealthCheckFailure < 99) origin.HealthCheckFailure++;
                            origin.HealthCheckSuccess = 0;

                            if (origin.Healthy && origin.HealthCheckFailure >= origin.UnhealthyThreshold)
                            {
                                origin.Healthy = false;
                                _Logging.Warn(_Header + "origin " + origin.Identifier + " is now unhealthy (timeout)");
                            }
                        }

                        _Logging.Debug(_Header + "health check failed for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl + ": " + hre.Message);
                    }
                    catch (HttpIOException ioe)
                    {
                        lock (origin.Lock)
                        {
                            if (origin.HealthCheckFailure < 99) origin.HealthCheckFailure++;
                            origin.HealthCheckSuccess = 0;

                            if (origin.Healthy && origin.HealthCheckFailure >= origin.UnhealthyThreshold)
                            {
                                origin.Healthy = false;
                                _Logging.Warn(_Header + "origin " + origin.Identifier + " is now unhealthy (timeout)");
                            }
                        }

                        _Logging.Debug(_Header + "health check failed for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl + ": " + ioe.Message);
                    }
                    catch (SocketException se)
                    {
                        lock (origin.Lock)
                        {
                            if (origin.HealthCheckFailure < 99) origin.HealthCheckFailure++;
                            origin.HealthCheckSuccess = 0;

                            if (origin.Healthy && origin.HealthCheckFailure >= origin.UnhealthyThreshold)
                            {
                                origin.Healthy = false;
                                _Logging.Warn(_Header + "origin " + origin.Identifier + " is now unhealthy (timeout)");
                            }
                        }

                        _Logging.Debug(_Header + "health check failed for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl + ": " + se.Message);
                    }
                    catch (TaskCanceledException)
                    {
                        // Expected when cancellation is requested or timeout occurs
                        if (!token.IsCancellationRequested)
                        {
                            // This was a timeout, not a cancellation
                            lock (origin.Lock)
                            {
                                if (origin.HealthCheckFailure < 99) origin.HealthCheckFailure++;
                                origin.HealthCheckSuccess = 0;

                                if (origin.Healthy && origin.HealthCheckFailure >= origin.UnhealthyThreshold)
                                {
                                    origin.Healthy = false;
                                    _Logging.Warn(_Header + "origin " + origin.Identifier + " is now unhealthy (timeout)");
                                }
                            }

                            _Logging.Debug(_Header + "health check timeout for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested or timeout occurs
                        if (!token.IsCancellationRequested)
                        {
                            // This was a timeout, not a cancellation
                            lock (origin.Lock)
                            {
                                if (origin.HealthCheckFailure < 99) origin.HealthCheckFailure++;
                                origin.HealthCheckSuccess = 0;

                                if (origin.Healthy && origin.HealthCheckFailure >= origin.UnhealthyThreshold)
                                {
                                    origin.Healthy = false;
                                    _Logging.Warn(_Header + "origin " + origin.Identifier + " is now unhealthy (timeout)");
                                }
                            }

                            _Logging.Debug(_Header + "health check timeout for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl);
                        }
                    }
                    catch (Exception e)
                    {
                        lock (origin.Lock)
                        {
                            if (origin.HealthCheckFailure < 99) origin.HealthCheckFailure++;
                            origin.HealthCheckSuccess = 0;

                            if (origin.Healthy && origin.HealthCheckFailure >= origin.UnhealthyThreshold)
                            {
                                origin.Healthy = false;
                                _Logging.Warn(_Header + "origin " + origin.Identifier + " is now unhealthy (" + e.GetType().Name + ")");
                            }
                        }

                        _Logging.Debug(_Header + "health check exception for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl + Environment.NewLine + e.ToString());
                    }
                }
            }

            _Logging.Debug(_Header + "stopping healthcheck task for origin " + origin.Identifier + " " + origin.Name + " " + healthCheckUrl);
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
