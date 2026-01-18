#nullable enable

namespace Switchboard.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core.Client;
    using Switchboard.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// Result of a settings import operation.
    /// </summary>
    public class ImportResult
    {
        /// <summary>
        /// Number of endpoints imported.
        /// </summary>
        public int EndpointsImported { get; set; } = 0;

        /// <summary>
        /// Number of origins imported.
        /// </summary>
        public int OriginsImported { get; set; } = 0;

        /// <summary>
        /// Number of routes imported.
        /// </summary>
        public int RoutesImported { get; set; } = 0;

        /// <summary>
        /// Number of endpoint-origin mappings imported.
        /// </summary>
        public int MappingsImported { get; set; } = 0;

        /// <summary>
        /// Number of URL rewrites imported.
        /// </summary>
        public int RewritesImported { get; set; } = 0;

        /// <summary>
        /// Number of blocked headers imported.
        /// </summary>
        public int BlockedHeadersImported { get; set; } = 0;

        /// <summary>
        /// Number of endpoints skipped (already exist).
        /// </summary>
        public int EndpointsSkipped { get; set; } = 0;

        /// <summary>
        /// Number of origins skipped (already exist).
        /// </summary>
        public int OriginsSkipped { get; set; } = 0;
    }

    /// <summary>
    /// Service to import configuration from SwitchboardSettings (sb.json) into the database.
    /// </summary>
    public class SettingsImportService
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private static string _Header = "[SettingsImportService] ";
        private readonly SwitchboardSettings _Settings;
        private readonly SwitchboardClient _Client;
        private readonly LoggingModule _Logging;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the settings import service.
        /// </summary>
        /// <param name="settings">Switchboard settings.</param>
        /// <param name="client">Switchboard client for database operations.</param>
        /// <param name="logging">Logging module.</param>
        public SettingsImportService(
            SwitchboardSettings settings,
            SwitchboardClient client,
            LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Client = client ?? throw new ArgumentNullException(nameof(client));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Check if there are any items in the settings file to potentially import.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if there are endpoints or origins in the settings file.</returns>
        public Task<bool> HasItemsToImportAsync(CancellationToken token = default)
        {
            bool hasItems = (_Settings.Endpoints?.Count > 0) || (_Settings.Origins?.Count > 0);
            return Task.FromResult(hasItems);
        }

        /// <summary>
        /// Import configuration from settings to database.
        /// Only imports items that don't already exist in the database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Import result with counts of imported items.</returns>
        public async Task<ImportResult> ImportAsync(CancellationToken token = default)
        {
            ImportResult result = new ImportResult();

            // Import origins first (endpoints reference them)
            if (_Settings.Origins != null)
            {
                foreach (OriginServer origin in _Settings.Origins)
                {
                    token.ThrowIfCancellationRequested();
                    await ImportOriginAsync(origin, result, token).ConfigureAwait(false);
                }
            }

            // Then import endpoints
            if (_Settings.Endpoints != null)
            {
                foreach (ApiEndpoint endpoint in _Settings.Endpoints)
                {
                    token.ThrowIfCancellationRequested();
                    await ImportEndpointAsync(endpoint, result, token).ConfigureAwait(false);
                }
            }

            // Import global blocked headers
            if (_Settings.BlockedHeaders != null)
            {
                foreach (string header in _Settings.BlockedHeaders)
                {
                    token.ThrowIfCancellationRequested();
                    await ImportBlockedHeaderAsync(header, result, token).ConfigureAwait(false);
                }
            }

            return result;
        }

        #endregion

        #region Private-Methods

        private async Task ImportOriginAsync(OriginServer origin, ImportResult result, CancellationToken token)
        {
            if (String.IsNullOrEmpty(origin.Identifier))
            {
                _Logging.Warn(_Header + "skipping origin with null identifier");
                return;
            }

            // Check if already exists
            OriginServerConfig? existing = await _Client.OriginServers
                .GetByIdentifierAsync(origin.Identifier, token).ConfigureAwait(false);

            if (existing != null)
            {
                _Logging.Debug(_Header + "origin '" + origin.Identifier + "' already exists, skipping");
                result.OriginsSkipped++;
                return;
            }

            // Create new origin
            OriginServerConfig config = MapOriginToConfig(origin);
            await _Client.OriginServers.CreateAsync(config, token).ConfigureAwait(false);
            result.OriginsImported++;

            _Logging.Debug(_Header + "imported origin '" + origin.Identifier + "'");
        }

        private async Task ImportEndpointAsync(ApiEndpoint endpoint, ImportResult result, CancellationToken token)
        {
            if (String.IsNullOrEmpty(endpoint.Identifier))
            {
                _Logging.Warn(_Header + "skipping endpoint with null identifier");
                return;
            }

            // Check if already exists
            ApiEndpointConfig? existing = await _Client.ApiEndpoints
                .GetByIdentifierAsync(endpoint.Identifier, token).ConfigureAwait(false);

            if (existing != null)
            {
                _Logging.Debug(_Header + "endpoint '" + endpoint.Identifier + "' already exists, skipping");
                result.EndpointsSkipped++;
                return;
            }

            // Create new endpoint
            ApiEndpointConfig config = MapEndpointToConfig(endpoint);
            ApiEndpointConfig created = await _Client.ApiEndpoints.CreateAsync(config, token).ConfigureAwait(false);
            result.EndpointsImported++;

            _Logging.Debug(_Header + "imported endpoint '" + endpoint.Identifier + "'");

            // Import unauthenticated routes
            if (endpoint.Unauthenticated?.ParameterizedUrls != null)
            {
                foreach (KeyValuePair<string, List<string>> methodRoutes in endpoint.Unauthenticated.ParameterizedUrls)
                {
                    string httpMethod = methodRoutes.Key;
                    foreach (string urlPattern in methodRoutes.Value)
                    {
                        EndpointRoute route = new EndpointRoute(
                            endpoint.Identifier,
                            httpMethod,
                            urlPattern,
                            requiresAuthentication: false)
                        {
                            EndpointGUID = created.GUID
                        };
                        await _Client.EndpointRoutes.CreateAsync(route, token).ConfigureAwait(false);
                        result.RoutesImported++;
                    }
                }
            }

            // Import authenticated routes
            if (endpoint.Authenticated?.ParameterizedUrls != null)
            {
                foreach (KeyValuePair<string, List<string>> methodRoutes in endpoint.Authenticated.ParameterizedUrls)
                {
                    string httpMethod = methodRoutes.Key;
                    foreach (string urlPattern in methodRoutes.Value)
                    {
                        EndpointRoute route = new EndpointRoute(
                            endpoint.Identifier,
                            httpMethod,
                            urlPattern,
                            requiresAuthentication: true)
                        {
                            EndpointGUID = created.GUID
                        };
                        await _Client.EndpointRoutes.CreateAsync(route, token).ConfigureAwait(false);
                        result.RoutesImported++;
                    }
                }
            }

            // Import URL rewrites
            if (endpoint.RewriteUrls != null)
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> methodRewrites in endpoint.RewriteUrls)
                {
                    string httpMethod = methodRewrites.Key;
                    foreach (KeyValuePair<string, string> rewrite in methodRewrites.Value)
                    {
                        UrlRewrite urlRewrite = new UrlRewrite(
                            endpoint.Identifier,
                            httpMethod,
                            rewrite.Key,
                            rewrite.Value)
                        {
                            EndpointGUID = created.GUID
                        };
                        await _Client.UrlRewrites.CreateAsync(urlRewrite, token).ConfigureAwait(false);
                        result.RewritesImported++;
                    }
                }
            }

            // Import origin server mappings
            if (endpoint.OriginServers != null)
            {
                int sortOrder = 0;
                foreach (string originIdentifier in endpoint.OriginServers)
                {
                    if (String.IsNullOrEmpty(originIdentifier))
                        continue;

                    // Look up the origin to get its GUID
                    OriginServerConfig? originConfig = await _Client.OriginServers
                        .GetByIdentifierAsync(originIdentifier, token).ConfigureAwait(false);

                    if (originConfig == null)
                    {
                        _Logging.Warn(_Header + "endpoint '" + endpoint.Identifier
                            + "' references unknown origin '" + originIdentifier + "', skipping mapping");
                        continue;
                    }

                    EndpointOriginMapping mapping = new EndpointOriginMapping(
                        endpoint.Identifier,
                        originIdentifier,
                        sortOrder)
                    {
                        EndpointGUID = created.GUID,
                        OriginGUID = originConfig.GUID
                    };
                    await _Client.EndpointOriginMappings.CreateAsync(mapping, token).ConfigureAwait(false);
                    result.MappingsImported++;
                    sortOrder++;
                }
            }
        }

        private async Task ImportBlockedHeaderAsync(string headerName, ImportResult result, CancellationToken token)
        {
            if (String.IsNullOrEmpty(headerName))
                return;

            // Check if already exists
            bool exists = await _Client.BlockedHeaders.IsBlockedAsync(headerName, token).ConfigureAwait(false);
            if (exists)
                return;

            BlockedHeader header = new BlockedHeader(headerName);
            await _Client.BlockedHeaders.CreateAsync(header, token).ConfigureAwait(false);
            result.BlockedHeadersImported++;
        }

        private OriginServerConfig MapOriginToConfig(OriginServer origin)
        {
            return new OriginServerConfig(origin.Identifier)
            {
                Name = origin.Name,
                Hostname = origin.Hostname,
                Port = origin.Port,
                Ssl = origin.Ssl,
                HealthCheckIntervalMs = origin.HealthCheckIntervalMs,
                HealthCheckMethod = origin.HealthCheckMethod.ToString(),
                HealthCheckUrl = origin.HealthCheckUrl,
                UnhealthyThreshold = origin.UnhealthyThreshold,
                HealthyThreshold = origin.HealthyThreshold,
                MaxParallelRequests = origin.MaxParallelRequests,
                RateLimitRequestsThreshold = origin.RateLimitRequestsThreshold,
                LogRequestBody = origin.LogRequestBody,
                LogResponseBody = origin.LogResponseBody,
                CaptureRequestBody = origin.CaptureRequestBody,
                CaptureResponseBody = origin.CaptureResponseBody,
                CaptureRequestHeaders = origin.CaptureRequestHeaders,
                CaptureResponseHeaders = origin.CaptureResponseHeaders,
                MaxCaptureRequestBodySize = origin.MaxCaptureRequestBodySize,
                MaxCaptureResponseBodySize = origin.MaxCaptureResponseBodySize
            };
        }

        private ApiEndpointConfig MapEndpointToConfig(ApiEndpoint endpoint)
        {
            return new ApiEndpointConfig(endpoint.Identifier)
            {
                Name = endpoint.Name,
                TimeoutMs = endpoint.TimeoutMs,
                LoadBalancingMode = endpoint.LoadBalancing.ToString(),
                BlockHttp10 = endpoint.BlockHttp10,
                MaxRequestBodySize = endpoint.MaxRequestBodySize,
                LogRequestFull = endpoint.LogRequestFull,
                LogRequestBody = endpoint.LogRequestBody,
                LogResponseBody = endpoint.LogResponseBody,
                IncludeAuthContextHeader = endpoint.IncludeAuthContextHeader,
                AuthContextHeader = endpoint.AuthContextHeader,
                UseGlobalBlockedHeaders = endpoint.UseGlobalBlockedHeaders,
                CaptureRequestBody = endpoint.CaptureRequestBody,
                CaptureResponseBody = endpoint.CaptureResponseBody,
                CaptureRequestHeaders = endpoint.CaptureRequestHeaders,
                CaptureResponseHeaders = endpoint.CaptureResponseHeaders,
                MaxCaptureRequestBodySize = endpoint.MaxCaptureRequestBodySize,
                MaxCaptureResponseBodySize = endpoint.MaxCaptureResponseBodySize
            };
        }

        #endregion
    }
}
