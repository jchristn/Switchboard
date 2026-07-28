namespace Switchboard.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core;
    using Switchboard.Core.Client;
    using Switchboard.Core.Models;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Projects database-managed configuration (created and edited through the management API and
    /// dashboard) into the live in-memory settings the gateway and health-check services consume.
    /// The settings loaded from the configuration file at startup are treated as an immutable
    /// baseline; database-only resources (those not present in the baseline) are merged on top so
    /// dashboard-created origins and endpoints take effect without a restart. A background monitor
    /// polls for changes and reloads only when the projected configuration actually changes.
    /// </summary>
    public class ConfigurationReloadService : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Logging module.
        /// </summary>
        public LoggingModule Logging
        {
            get => _Logging;
            set => _Logging = value ?? throw new ArgumentNullException(nameof(Logging));
        }

        /// <summary>
        /// Health-check service to reconcile after each reload so newly added origins begin health
        /// checks and removed origins stop. May be null (for example during the initial hydrate,
        /// before the health-check service is constructed).
        /// </summary>
        public HealthCheckService HealthCheckService { get; set; } = null;

        /// <summary>
        /// Interval, in milliseconds, at which the background monitor polls the database for
        /// configuration changes. Default is 3000. Minimum is 500.
        /// </summary>
        public int PollIntervalMs
        {
            get => _PollIntervalMs;
            set
            {
                if (value < 500) throw new ArgumentOutOfRangeException(nameof(PollIntervalMs));
                _PollIntervalMs = value;
            }
        }

        #endregion

        #region Private-Members

        private readonly string _Header = "[ConfigurationReloadService] ";
        private SwitchboardSettings _Settings = null;
        private SwitchboardClient _Client = null;
        private LoggingModule _Logging = null;
        private int _PollIntervalMs = 3000;

        private readonly List<OriginServer> _BaselineOrigins;
        private readonly List<ApiEndpoint> _BaselineEndpoints;
        private readonly List<string> _BaselineBlockedHeaders;
        private readonly HashSet<string> _BaselineOriginIdentifiers;
        private readonly HashSet<string> _BaselineEndpointIdentifiers;

        private readonly object _ReloadLock = new object();
        private string _LastSignature = null;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private Task _MonitorTask = null;
        private bool _IsDisposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Settings. The current Origins/Endpoints/BlockedHeaders are captured as the immutable baseline.</param>
        /// <param name="client">Switchboard client for database access.</param>
        /// <param name="logging">Logging.</param>
        public ConfigurationReloadService(
            SwitchboardSettings settings,
            SwitchboardClient client,
            LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Client = client ?? throw new ArgumentNullException(nameof(client));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));

            _BaselineOrigins = _Settings.Origins != null ? new List<OriginServer>(_Settings.Origins) : new List<OriginServer>();
            _BaselineEndpoints = _Settings.Endpoints != null ? new List<ApiEndpoint>(_Settings.Endpoints) : new List<ApiEndpoint>();
            _BaselineBlockedHeaders = _Settings.BlockedHeaders != null ? new List<string>(_Settings.BlockedHeaders) : new List<string>();

            _BaselineOriginIdentifiers = new HashSet<string>(_BaselineOrigins.Where(o => o.Identifier != null).Select(o => o.Identifier));
            _BaselineEndpointIdentifiers = new HashSet<string>(_BaselineEndpoints.Where(e => e.Identifier != null).Select(e => e.Identifier));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the background monitor that reloads configuration when the database changes.
        /// </summary>
        public void StartMonitor()
        {
            if (_MonitorTask != null) return;
            _MonitorTask = Task.Run(() => MonitorLoop(_TokenSource.Token));
        }

        /// <summary>
        /// Reload configuration from the database into settings, merging database-only resources on
        /// top of the baseline. No-op (returns false) when the projected configuration is unchanged
        /// since the last reload.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if settings were updated; false if nothing changed.</returns>
        public async Task<bool> ReloadAsync(CancellationToken token = default)
        {
            List<OriginServerConfig> originConfigs = await _Client.OriginServers.GetAllAsync(null, 0, null, token).ConfigureAwait(false);
            List<ApiEndpointConfig> endpointConfigs = await _Client.ApiEndpoints.GetAllAsync(null, 0, null, token).ConfigureAwait(false);
            List<EndpointRoute> routes = await _Client.EndpointRoutes.GetAllAsync(0, null, token).ConfigureAwait(false);
            List<EndpointOriginMapping> mappings = await _Client.EndpointOriginMappings.GetAllAsync(0, null, token).ConfigureAwait(false);
            List<UrlRewrite> rewrites = await _Client.UrlRewrites.GetAllAsync(0, null, token).ConfigureAwait(false);
            List<BlockedHeader> blockedHeaders = await _Client.BlockedHeaders.GetAllAsync(0, null, token).ConfigureAwait(false);

            string signature = ComputeSignature(originConfigs, endpointConfigs, routes, mappings, rewrites, blockedHeaders);

            lock (_ReloadLock)
            {
                if (String.Equals(signature, _LastSignature, StringComparison.Ordinal)) return false;

                // Preserve existing runtime OriginServer instances (and their health state) by identifier.
                Dictionary<string, OriginServer> existingOrigins = new Dictionary<string, OriginServer>();
                if (_Settings.Origins != null)
                {
                    foreach (OriginServer o in _Settings.Origins)
                        if (o.Identifier != null) existingOrigins[o.Identifier] = o;
                }

                #region Origins

                List<OriginServer> newOrigins = new List<OriginServer>();

                // Baseline origins keep their exact instances.
                foreach (OriginServer baseline in _BaselineOrigins)
                    newOrigins.Add(baseline);

                // Database-only origins are projected and merged.
                foreach (OriginServerConfig cfg in originConfigs)
                {
                    if (cfg.Identifier == null || _BaselineOriginIdentifiers.Contains(cfg.Identifier)) continue;

                    OriginServer origin = existingOrigins.TryGetValue(cfg.Identifier, out OriginServer reuse)
                        ? reuse
                        : new OriginServer { Identifier = cfg.Identifier };

                    ApplyOriginConfig(origin, cfg);
                    newOrigins.Add(origin);
                }

                #endregion

                #region Endpoints

                List<ApiEndpoint> newEndpoints = new List<ApiEndpoint>();

                // Baseline endpoints keep their exact instances.
                foreach (ApiEndpoint baseline in _BaselineEndpoints)
                    newEndpoints.Add(baseline);

                // Database-only endpoints are projected from their config, routes, mappings, and rewrites.
                foreach (ApiEndpointConfig cfg in endpointConfigs)
                {
                    if (cfg.Identifier == null || _BaselineEndpointIdentifiers.Contains(cfg.Identifier)) continue;
                    newEndpoints.Add(ProjectEndpoint(cfg, routes, mappings, rewrites));
                }

                #endregion

                #region Blocked-Headers

                List<string> newBlockedHeaders = new List<string>(_BaselineBlockedHeaders);
                HashSet<string> seenHeaders = new HashSet<string>(_BaselineBlockedHeaders, StringComparer.OrdinalIgnoreCase);
                foreach (BlockedHeader header in blockedHeaders)
                {
                    if (String.IsNullOrEmpty(header.HeaderName)) continue;
                    if (seenHeaders.Add(header.HeaderName)) newBlockedHeaders.Add(header.HeaderName);
                }

                #endregion

                _Settings.Origins = newOrigins;
                _Settings.Endpoints = newEndpoints;
                _Settings.BlockedHeaders = newBlockedHeaders;
                _LastSignature = signature;

                _Logging?.Debug(_Header + "reloaded configuration: "
                    + newOrigins.Count + " origin(s), "
                    + newEndpoints.Count + " endpoint(s), "
                    + newBlockedHeaders.Count + " blocked header(s)");
            }

            HealthCheckService?.SyncOrigins();
            return true;
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            if (_IsDisposed) return;

            try { _TokenSource.Cancel(); } catch { }
            try { _TokenSource.Dispose(); } catch { }

            _MonitorTask = null;
            _Settings = null;
            _Client = null;
            _Logging = null;
            _IsDisposed = true;

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_PollIntervalMs, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) break;
                    await ReloadAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    _Logging?.Warn(_Header + "error during configuration reload: " + e.Message);
                }
            }
        }

        private static void ApplyOriginConfig(OriginServer origin, OriginServerConfig cfg)
        {
            origin.Name = cfg.Name;
            origin.Hostname = cfg.Hostname;
            origin.Port = cfg.Port;
            origin.Ssl = cfg.Ssl;
            origin.HealthCheckIntervalMs = cfg.HealthCheckIntervalMs;
            origin.HealthCheckMethod = Enum.TryParse(cfg.HealthCheckMethod, true, out HttpMethod method) ? method : HttpMethod.GET;
            origin.HealthCheckUrl = cfg.HealthCheckUrl;
            origin.UnhealthyThreshold = cfg.UnhealthyThreshold;
            origin.HealthyThreshold = cfg.HealthyThreshold;
            origin.MaxParallelRequests = cfg.MaxParallelRequests;
            origin.RateLimitRequestsThreshold = cfg.RateLimitRequestsThreshold;
            origin.LogRequestBody = cfg.LogRequestBody;
            origin.LogResponseBody = cfg.LogResponseBody;
            origin.CaptureRequestBody = cfg.CaptureRequestBody;
            origin.CaptureResponseBody = cfg.CaptureResponseBody;
            origin.CaptureRequestHeaders = cfg.CaptureRequestHeaders;
            origin.CaptureResponseHeaders = cfg.CaptureResponseHeaders;
            origin.MaxCaptureRequestBodySize = cfg.MaxCaptureRequestBodySize;
            origin.MaxCaptureResponseBodySize = cfg.MaxCaptureResponseBodySize;
        }

        private static ApiEndpoint ProjectEndpoint(
            ApiEndpointConfig cfg,
            List<EndpointRoute> routes,
            List<EndpointOriginMapping> mappings,
            List<UrlRewrite> rewrites)
        {
            ApiEndpoint ep = new ApiEndpoint
            {
                Identifier = cfg.Identifier,
                Name = cfg.Name,
                TimeoutMs = cfg.TimeoutMs,
                LoadBalancing = Enum.TryParse(cfg.LoadBalancingMode, true, out LoadBalancingMode lb) ? lb : LoadBalancingMode.RoundRobin,
                IncludeAuthContextHeader = cfg.IncludeAuthContextHeader,
                BlockHttp10 = cfg.BlockHttp10,
                LogRequestFull = cfg.LogRequestFull,
                LogRequestBody = cfg.LogRequestBody,
                LogResponseBody = cfg.LogResponseBody,
                MaxRequestBodySize = cfg.MaxRequestBodySize,
                UseGlobalBlockedHeaders = cfg.UseGlobalBlockedHeaders,
                CaptureRequestBody = cfg.CaptureRequestBody,
                CaptureResponseBody = cfg.CaptureResponseBody,
                CaptureRequestHeaders = cfg.CaptureRequestHeaders,
                CaptureResponseHeaders = cfg.CaptureResponseHeaders,
                MaxCaptureRequestBodySize = cfg.MaxCaptureRequestBodySize,
                MaxCaptureResponseBodySize = cfg.MaxCaptureResponseBodySize
            };

            if (!String.IsNullOrEmpty(cfg.AuthContextHeader)) ep.AuthContextHeader = cfg.AuthContextHeader;

            // Routes -> Unauthenticated/Authenticated parameterized URLs (method -> list of URL patterns).
            foreach (EndpointRoute route in routes
                .Where(r => r.EndpointIdentifier == cfg.Identifier)
                .OrderBy(r => r.SortOrder))
            {
                ApiEndpointGroup group = route.RequiresAuthentication ? ep.Authenticated : ep.Unauthenticated;
                if (!group.ParameterizedUrls.TryGetValue(route.HttpMethod, out List<string> urls))
                {
                    urls = new List<string>();
                    group.ParameterizedUrls[route.HttpMethod] = urls;
                }
                if (!urls.Contains(route.UrlPattern)) urls.Add(route.UrlPattern);
            }

            // URL rewrites -> method -> (source -> target).
            foreach (UrlRewrite rewrite in rewrites
                .Where(r => r.EndpointIdentifier == cfg.Identifier)
                .OrderBy(r => r.SortOrder))
            {
                if (!ep.RewriteUrls.TryGetValue(rewrite.HttpMethod, out Dictionary<string, string> map))
                {
                    map = new Dictionary<string, string>();
                    ep.RewriteUrls[rewrite.HttpMethod] = map;
                }
                map[rewrite.SourcePattern] = rewrite.TargetPattern;
            }

            // Origin mappings -> ordered list of origin identifiers.
            ep.OriginServers = mappings
                .Where(m => m.EndpointIdentifier == cfg.Identifier)
                .OrderBy(m => m.SortOrder)
                .Select(m => m.OriginIdentifier)
                .Where(id => !String.IsNullOrEmpty(id))
                .ToList();

            return ep;
        }

        private static string ComputeSignature(
            List<OriginServerConfig> origins,
            List<ApiEndpointConfig> endpoints,
            List<EndpointRoute> routes,
            List<EndpointOriginMapping> mappings,
            List<UrlRewrite> rewrites,
            List<BlockedHeader> blockedHeaders)
        {
            StringBuilder sb = new StringBuilder();

            foreach (OriginServerConfig o in origins.OrderBy(x => x.Identifier, StringComparer.Ordinal))
            {
                sb.Append("O|").Append(o.Identifier).Append('|').Append(o.Name).Append('|')
                    .Append(o.Hostname).Append('|').Append(o.Port).Append('|').Append(o.Ssl).Append('|')
                    .Append(o.HealthCheckMethod).Append('|').Append(o.HealthCheckUrl).Append('|')
                    .Append(o.HealthCheckIntervalMs).Append('|').Append(o.HealthyThreshold).Append('|')
                    .Append(o.UnhealthyThreshold).Append('|').Append(o.MaxParallelRequests).Append('|')
                    .Append(o.RateLimitRequestsThreshold).Append(';');
            }

            foreach (ApiEndpointConfig e in endpoints.OrderBy(x => x.Identifier, StringComparer.Ordinal))
            {
                sb.Append("E|").Append(e.Identifier).Append('|').Append(e.Name).Append('|')
                    .Append(e.LoadBalancingMode).Append('|').Append(e.TimeoutMs).Append('|')
                    .Append(e.BlockHttp10).Append('|').Append(e.MaxRequestBodySize).Append('|')
                    .Append(e.IncludeAuthContextHeader).Append('|').Append(e.AuthContextHeader).Append('|')
                    .Append(e.UseGlobalBlockedHeaders).Append(';');
            }

            foreach (EndpointRoute r in routes
                .OrderBy(x => x.EndpointIdentifier, StringComparer.Ordinal).ThenBy(x => x.Id))
            {
                sb.Append("R|").Append(r.EndpointIdentifier).Append('|').Append(r.HttpMethod).Append('|')
                    .Append(r.UrlPattern).Append('|').Append(r.RequiresAuthentication).Append('|')
                    .Append(r.SortOrder).Append(';');
            }

            foreach (EndpointOriginMapping m in mappings
                .OrderBy(x => x.EndpointIdentifier, StringComparer.Ordinal).ThenBy(x => x.SortOrder).ThenBy(x => x.OriginIdentifier, StringComparer.Ordinal))
            {
                sb.Append("M|").Append(m.EndpointIdentifier).Append('|').Append(m.OriginIdentifier).Append('|')
                    .Append(m.SortOrder).Append(';');
            }

            foreach (UrlRewrite w in rewrites
                .OrderBy(x => x.EndpointIdentifier, StringComparer.Ordinal).ThenBy(x => x.Id))
            {
                sb.Append("W|").Append(w.EndpointIdentifier).Append('|').Append(w.HttpMethod).Append('|')
                    .Append(w.SourcePattern).Append('|').Append(w.TargetPattern).Append('|').Append(w.SortOrder).Append(';');
            }

            foreach (BlockedHeader h in blockedHeaders.OrderBy(x => x.HeaderName, StringComparer.Ordinal))
            {
                sb.Append("H|").Append(h.HeaderName).Append(';');
            }

            return sb.ToString();
        }

        #endregion
    }
}
