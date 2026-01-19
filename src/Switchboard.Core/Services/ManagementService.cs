#nullable enable

using Switchboard;

namespace Switchboard.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using SyslogLogging;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using Switchboard.Core.Client;
    using Switchboard.Core.Client.Interfaces;
    using Switchboard.Core.Models;
    using Switchboard.Core.Settings;

    /// <summary>
    /// Management API service.
    /// Provides REST endpoints for managing Switchboard configuration.
    /// </summary>
    public class ManagementService : IDisposable
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

        #endregion

        #region Private-Members

        private readonly ManagementSettings _Settings;
        private readonly SwitchboardClient _Client;
        private LoggingModule _Logging;
        private readonly string _Header = "[ManagementService] ";
        private bool _Disposed = false;
        private string? _CachedOpenApiDocument = null;
        private string? _CachedSwaggerHtml = null;
        private string _ServerUrl = "";

        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate management service.
        /// </summary>
        /// <param name="settings">Management settings.</param>
        /// <param name="client">Switchboard client.</param>
        /// <param name="logging">Logging module.</param>
        public ManagementService(
            ManagementSettings settings,
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
        /// Initialize routes on the webserver.
        /// </summary>
        /// <param name="webserver">Webserver instance.</param>
        /// <param name="serverUrl">Server URL for OpenAPI documentation (e.g., "http://localhost:8000").</param>
        public void InitializeRoutes(Webserver webserver, string serverUrl = "")
        {
            if (webserver == null)
                throw new ArgumentNullException(nameof(webserver));

            if (!_Settings.Enable)
            {
                _Logging.Info(_Header + "management API is disabled");
                return;
            }

            _ServerUrl = serverUrl ?? "";
            string basePath = _Settings.BasePath.TrimEnd('/');
            _Logging.Info(_Header + "initializing routes at " + basePath);

            // Origin servers
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/origins", GetOriginsAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, basePath + "/origins", CreateOriginAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, basePath + "/origins/{guid}", GetOriginAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.PUT, basePath + "/origins/{guid}", UpdateOriginAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.DELETE, basePath + "/origins/{guid}", DeleteOriginAsync);

            // API endpoints
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/endpoints", GetEndpointsAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, basePath + "/endpoints", CreateEndpointAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, basePath + "/endpoints/{guid}", GetEndpointAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.PUT, basePath + "/endpoints/{guid}", UpdateEndpointAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.DELETE, basePath + "/endpoints/{guid}", DeleteEndpointAsync);

            // Endpoint routes
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/routes", GetRoutesAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, basePath + "/routes", CreateRouteAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, basePath + "/routes/{id}", GetRouteAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.PUT, basePath + "/routes/{id}", UpdateRouteAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.DELETE, basePath + "/routes/{id}", DeleteRouteAsync);

            // Endpoint-origin mappings
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/mappings", GetMappingsAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, basePath + "/mappings", CreateMappingAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, basePath + "/mappings/{id}", GetMappingAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.DELETE, basePath + "/mappings/{id}", DeleteMappingAsync);

            // URL rewrites
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/rewrites", GetRewritesAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, basePath + "/rewrites", CreateRewriteAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, basePath + "/rewrites/{id}", GetRewriteAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.PUT, basePath + "/rewrites/{id}", UpdateRewriteAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.DELETE, basePath + "/rewrites/{id}", DeleteRewriteAsync);

            // Blocked headers
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/headers", GetBlockedHeadersAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, basePath + "/headers", CreateBlockedHeaderAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, basePath + "/headers/{id}", GetBlockedHeaderAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.DELETE, basePath + "/headers/{id}", DeleteBlockedHeaderAsync);

            // Users
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/users", GetUsersAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, basePath + "/users", CreateUserAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, basePath + "/users/{guid}", GetUserAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.PUT, basePath + "/users/{guid}", UpdateUserAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.DELETE, basePath + "/users/{guid}", DeleteUserAsync);

            // Credentials
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/credentials", GetCredentialsAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, basePath + "/credentials", CreateCredentialAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, basePath + "/credentials/{guid}", GetCredentialAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.PUT, basePath + "/credentials/{guid}", UpdateCredentialAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.DELETE, basePath + "/credentials/{guid}", DeleteCredentialAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.POST, basePath + "/credentials/{guid}/regenerate", RegenerateCredentialAsync);

            // Request history
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/history", GetRequestHistoryAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/history/recent", GetRecentHistoryAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/history/failed", GetFailedHistoryAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, basePath + "/history/{id}", GetHistoryByIdAsync);
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.DELETE, basePath + "/history/{id}", DeleteHistoryAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, basePath + "/history/cleanup", RunHistoryCleanupAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/history/stats", GetHistoryStatsAsync);

            // Health check
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/health", GetHealthAsync);

            // Current user
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, basePath + "/me", GetCurrentUserAsync);

            // OpenAPI and Swagger UI for Management API (at root path for discoverability)
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/openapi.json", GetManagementOpenApiAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/swagger", GetManagementSwaggerUiAsync);

            // Pre-generate OpenAPI document
            try
            {
                SwitchboardOpenApiDocumentGenerator generator = new SwitchboardOpenApiDocumentGenerator();
                _CachedOpenApiDocument = generator.GenerateManagementApiDocument(basePath, _ServerUrl);
                _CachedSwaggerHtml = SwaggerUiHandler.GenerateHtml("/openapi.json", "Switchboard Management API");
                _Logging.Debug(_Header + "Management API OpenAPI document generated successfully");
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to generate Management API OpenAPI document: " + ex.Message);
            }

            _Logging.Info(_Header + "routes initialized successfully");
            _Logging.Info(_Header + "management API OpenAPI document available at /openapi.json");
            _Logging.Info(_Header + "management API Swagger UI available at /swagger");
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

        #region Private-Methods-Auth

        private bool AuthenticateRequest(HttpContextBase ctx)
        {
            if (!_Settings.RequireAuthentication)
                return true;

            string? authHeader = ctx.Request.Headers.Get("Authorization");
            if (String.IsNullOrWhiteSpace(authHeader))
                return false;

            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return false;

            string token = authHeader.Substring(7).Trim();
            if (String.IsNullOrWhiteSpace(token))
                return false;

            // Check for static admin token from settings
            if (!String.IsNullOrWhiteSpace(_Settings.AdminToken) && token == _Settings.AdminToken)
                return true;

            // Validate against database credentials
            try
            {
                UserMaster? user = _Client.Credentials.GetUserByBearerTokenAsync(token, ctx.Token).GetAwaiter().GetResult();
                return user != null && user.Active;
            }
            catch
            {
                return false;
            }
        }

        private bool HasWriteAccess(HttpContextBase ctx)
        {
            if (!_Settings.RequireAuthentication)
                return true;

            string? authHeader = ctx.Request.Headers.Get("Authorization");
            if (String.IsNullOrWhiteSpace(authHeader))
                return false;

            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return false;

            string token = authHeader.Substring(7).Trim();
            if (String.IsNullOrWhiteSpace(token))
                return false;

            // Admin token has full access
            if (!String.IsNullOrWhiteSpace(_Settings.AdminToken) && token == _Settings.AdminToken)
                return true;

            // Check if the credential is read-only
            try
            {
                Credential? credential = _Client.Credentials.ValidateBearerTokenAsync(token, ctx.Token).GetAwaiter().GetResult();
                return credential != null && !credential.IsReadOnly;
            }
            catch
            {
                return false;
            }
        }

        private async Task SendUnauthorized(HttpContextBase ctx)
        {
            ApiErrorResponse error = new ApiErrorResponse(ApiErrorEnum.AuthenticationFailed);
            ctx.Response.StatusCode = error.StatusCode;
            ctx.Response.ContentType = "application/json";
            string response = JsonSerializer.Serialize(error, _JsonOptions);
            await ctx.Response.Send(response).ConfigureAwait(false);
        }

        private async Task SendForbidden(HttpContextBase ctx, string message)
        {
            ApiErrorResponse error = new ApiErrorResponse(ApiErrorEnum.AuthorizationFailed, description: message);
            ctx.Response.StatusCode = error.StatusCode;
            ctx.Response.ContentType = "application/json";
            string response = JsonSerializer.Serialize(error, _JsonOptions);
            await ctx.Response.Send(response).ConfigureAwait(false);
        }

        private async Task SendBadRequest(HttpContextBase ctx, string message)
        {
            ApiErrorResponse error = new ApiErrorResponse(ApiErrorEnum.BadRequest, description: message);
            ctx.Response.StatusCode = error.StatusCode;
            ctx.Response.ContentType = "application/json";
            string response = JsonSerializer.Serialize(error, _JsonOptions);
            await ctx.Response.Send(response).ConfigureAwait(false);
        }

        private async Task SendNotFound(HttpContextBase ctx, string message)
        {
            ApiErrorResponse error = new ApiErrorResponse(ApiErrorEnum.NotFound, description: message);
            ctx.Response.StatusCode = error.StatusCode;
            ctx.Response.ContentType = "application/json";
            string response = JsonSerializer.Serialize(error, _JsonOptions);
            await ctx.Response.Send(response).ConfigureAwait(false);
        }

        private async Task SendOk(HttpContextBase ctx, object? data = null)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            if (data != null)
            {
                string response = JsonSerializer.Serialize(data, _JsonOptions);
                await ctx.Response.Send(response).ConfigureAwait(false);
            }
            else
            {
                await ctx.Response.Send().ConfigureAwait(false);
            }
        }

        private async Task SendCreated(HttpContextBase ctx, object data)
        {
            ctx.Response.StatusCode = 201;
            ctx.Response.ContentType = "application/json";
            string response = JsonSerializer.Serialize(data, _JsonOptions);
            await ctx.Response.Send(response).ConfigureAwait(false);
        }

        private async Task SendNoContent(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send().ConfigureAwait(false);
        }

        private async Task SendError(HttpContextBase ctx, Exception ex)
        {
            _Logging.Warn(_Header + "exception: " + ex.Message);
            ApiErrorResponse error = new ApiErrorResponse(ApiErrorEnum.InternalError, description: ex.Message);
            ctx.Response.StatusCode = error.StatusCode;
            ctx.Response.ContentType = "application/json";
            string response = JsonSerializer.Serialize(error, _JsonOptions);
            await ctx.Response.Send(response).ConfigureAwait(false);
        }

        private async Task<T?> ReadBody<T>(HttpContextBase ctx) where T : class
        {
            try
            {
                if (ctx.Request.Data == null || ctx.Request.ContentLength < 1)
                {
                    _Logging.Warn(_Header + "no request body found");
                    return null;
                }

                byte[] data = new byte[ctx.Request.ContentLength];
                await ctx.Request.Data.ReadAsync(data, 0, (int)ctx.Request.ContentLength, ctx.Token).ConfigureAwait(false);
                string json = System.Text.Encoding.UTF8.GetString(data);
                return JsonSerializer.Deserialize<T>(json, _JsonOptions);
            }
            catch (Exception e)
            {
                _Logging.Warn(_Header + "exception while reading request body:" + Environment.NewLine + e.ToString());
                return null;
            }
        }

        #endregion

        #region Private-Methods-Origins

        private async Task GetOriginsAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? searchTerm = ctx.Request.Query.Elements["search"];
                int skip = Int32.TryParse(ctx.Request.Query.Elements["skip"], out int skipVal) ? skipVal : 0;
                int? take = Int32.TryParse(ctx.Request.Query.Elements["take"], out int takeVal) ? takeVal : (int?)null;

                List<OriginServerConfig> origins = await _Client.OriginServers.GetAllAsync(searchTerm, skip, take, ctx.Token).ConfigureAwait(false);
                await SendOk(ctx, origins).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task CreateOriginAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot create resources").ConfigureAwait(false); return; }

                OriginServerConfig? config = await ReadBody<OriginServerConfig>(ctx).ConfigureAwait(false);
                if (config == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                OriginServerConfig created = await _Client.OriginServers.CreateAsync(config, ctx.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "created origin server " + created.Identifier);
                await SendCreated(ctx, created).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task GetOriginAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                OriginServerConfig? config = await _Client.OriginServers.GetByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                if (config == null) { await SendNotFound(ctx, "Origin server not found").ConfigureAwait(false); return; }

                await SendOk(ctx, config).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task UpdateOriginAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot update resources").ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                OriginServerConfig? config = await ReadBody<OriginServerConfig>(ctx).ConfigureAwait(false);
                if (config == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                config.GUID = guid;
                OriginServerConfig updated = await _Client.OriginServers.UpdateAsync(config, ctx.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "updated origin server " + updated.Identifier);
                await SendOk(ctx, updated).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task DeleteOriginAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot delete resources").ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                OriginServerConfig? existing = await _Client.OriginServers.GetByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                if (existing == null) { await SendNotFound(ctx, "Origin server not found").ConfigureAwait(false); return; }

                await _Client.OriginServers.DeleteByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "deleted origin server " + existing.Identifier);
                await SendNoContent(ctx).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        #endregion

        #region Private-Methods-Endpoints

        private async Task GetEndpointsAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? searchTerm = ctx.Request.Query.Elements["search"];
                int skip = Int32.TryParse(ctx.Request.Query.Elements["skip"], out int skipVal) ? skipVal : 0;
                int? take = Int32.TryParse(ctx.Request.Query.Elements["take"], out int takeVal) ? takeVal : (int?)null;

                List<ApiEndpointConfig> endpoints = await _Client.ApiEndpoints.GetAllAsync(searchTerm, skip, take, ctx.Token).ConfigureAwait(false);
                await SendOk(ctx, endpoints).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task CreateEndpointAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot create resources").ConfigureAwait(false); return; }

                ApiEndpointConfig? config = await ReadBody<ApiEndpointConfig>(ctx).ConfigureAwait(false);
                if (config == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                ApiEndpointConfig created = await _Client.ApiEndpoints.CreateAsync(config, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "created endpoint " + created.Identifier);
                await SendCreated(ctx, created).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task GetEndpointAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                ApiEndpointConfig? config = await _Client.ApiEndpoints.GetByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                if (config == null) { await SendNotFound(ctx, "Endpoint not found").ConfigureAwait(false); return; }

                await SendOk(ctx, config).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task UpdateEndpointAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot update resources").ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                ApiEndpointConfig? config = await ReadBody<ApiEndpointConfig>(ctx).ConfigureAwait(false);
                if (config == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                config.GUID = guid;
                ApiEndpointConfig updated = await _Client.ApiEndpoints.UpdateAsync(config, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "updated endpoint " + updated.Identifier);
                await SendOk(ctx, updated).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task DeleteEndpointAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot delete resources").ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                ApiEndpointConfig? existing = await _Client.ApiEndpoints.GetByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                if (existing == null) { await SendNotFound(ctx, "Endpoint not found").ConfigureAwait(false); return; }

                await _Client.ApiEndpoints.DeleteByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "deleted endpoint " + existing.Identifier);
                await SendNoContent(ctx).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        #endregion

        #region Private-Methods-Routes

        private async Task GetRoutesAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                int skip = Int32.TryParse(ctx.Request.Query.Elements["skip"], out int skipVal) ? skipVal : 0;
                int? take = Int32.TryParse(ctx.Request.Query.Elements["take"], out int takeVal) ? takeVal : (int?)null;

                List<EndpointRoute> routes = await _Client.EndpointRoutes.GetAllAsync(skip, take, ctx.Token).ConfigureAwait(false);
                await SendOk(ctx, routes).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task CreateRouteAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot create resources").ConfigureAwait(false); return; }

                EndpointRoute? route = await ReadBody<EndpointRoute>(ctx).ConfigureAwait(false);
                if (route == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                EndpointRoute created = await _Client.EndpointRoutes.CreateAsync(route, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "created route " + created.Id);
                await SendCreated(ctx, created).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task GetRouteAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? idStr = ctx.Request.Url.Parameters["id"];
                if (!Int32.TryParse(idStr, out int id)) { await SendBadRequest(ctx, "Invalid ID").ConfigureAwait(false); return; }

                EndpointRoute? route = await _Client.EndpointRoutes.GetByIdAsync(id, ctx.Token).ConfigureAwait(false);
                if (route == null) { await SendNotFound(ctx, "Route not found").ConfigureAwait(false); return; }

                await SendOk(ctx, route).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task UpdateRouteAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot update resources").ConfigureAwait(false); return; }

                string? idStr = ctx.Request.Url.Parameters["id"];
                if (!Int32.TryParse(idStr, out int id)) { await SendBadRequest(ctx, "Invalid ID").ConfigureAwait(false); return; }

                EndpointRoute? route = await ReadBody<EndpointRoute>(ctx).ConfigureAwait(false);
                if (route == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                route.Id = id;
                EndpointRoute updated = await _Client.EndpointRoutes.UpdateAsync(route, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "updated route " + updated.Id);
                await SendOk(ctx, updated).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task DeleteRouteAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot delete resources").ConfigureAwait(false); return; }

                string? idStr = ctx.Request.Url.Parameters["id"];
                if (!Int32.TryParse(idStr, out int id)) { await SendBadRequest(ctx, "Invalid ID").ConfigureAwait(false); return; }

                EndpointRoute? existing = await _Client.EndpointRoutes.GetByIdAsync(id, ctx.Token).ConfigureAwait(false);
                if (existing == null) { await SendNotFound(ctx, "Route not found").ConfigureAwait(false); return; }

                await _Client.EndpointRoutes.DeleteByIdAsync(id, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "deleted route " + existing.Id);
                await SendNoContent(ctx).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        #endregion

        #region Private-Methods-Mappings

        private async Task GetMappingsAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                int skip = Int32.TryParse(ctx.Request.Query.Elements["skip"], out int skipVal) ? skipVal : 0;
                int? take = Int32.TryParse(ctx.Request.Query.Elements["take"], out int takeVal) ? takeVal : (int?)null;

                List<EndpointOriginMapping> mappings = await _Client.EndpointOriginMappings.GetAllAsync(skip, take, ctx.Token).ConfigureAwait(false);
                await SendOk(ctx, mappings).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task CreateMappingAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot create resources").ConfigureAwait(false); return; }

                EndpointOriginMapping? mapping = await ReadBody<EndpointOriginMapping>(ctx).ConfigureAwait(false);
                if (mapping == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                EndpointOriginMapping created = await _Client.EndpointOriginMappings.CreateAsync(mapping, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "created mapping " + created.Id);
                await SendCreated(ctx, created).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task GetMappingAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? idStr = ctx.Request.Url.Parameters["id"];
                if (!Int32.TryParse(idStr, out int id)) { await SendBadRequest(ctx, "Invalid ID").ConfigureAwait(false); return; }

                EndpointOriginMapping? mapping = await _Client.EndpointOriginMappings.GetByIdAsync(id, ctx.Token).ConfigureAwait(false);
                if (mapping == null) { await SendNotFound(ctx, "Mapping not found").ConfigureAwait(false); return; }

                await SendOk(ctx, mapping).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task DeleteMappingAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot delete resources").ConfigureAwait(false); return; }

                string? idStr = ctx.Request.Url.Parameters["id"];
                if (!Int32.TryParse(idStr, out int id)) { await SendBadRequest(ctx, "Invalid ID").ConfigureAwait(false); return; }

                EndpointOriginMapping? existing = await _Client.EndpointOriginMappings.GetByIdAsync(id, ctx.Token).ConfigureAwait(false);
                if (existing == null) { await SendNotFound(ctx, "Mapping not found").ConfigureAwait(false); return; }

                await _Client.EndpointOriginMappings.DeleteByIdAsync(id, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "deleted mapping " + existing.Id);
                await SendNoContent(ctx).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        #endregion

        #region Private-Methods-Rewrites

        private async Task GetRewritesAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                int skip = Int32.TryParse(ctx.Request.Query.Elements["skip"], out int skipVal) ? skipVal : 0;
                int? take = Int32.TryParse(ctx.Request.Query.Elements["take"], out int takeVal) ? takeVal : (int?)null;

                List<UrlRewrite> rewrites = await _Client.UrlRewrites.GetAllAsync(skip, take, ctx.Token).ConfigureAwait(false);
                await SendOk(ctx, rewrites).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task CreateRewriteAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot create resources").ConfigureAwait(false); return; }

                UrlRewrite? rewrite = await ReadBody<UrlRewrite>(ctx).ConfigureAwait(false);
                if (rewrite == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                UrlRewrite created = await _Client.UrlRewrites.CreateAsync(rewrite, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "created rewrite " + created.Id);
                await SendCreated(ctx, created).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task GetRewriteAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? idStr = ctx.Request.Url.Parameters["id"];
                if (!Int32.TryParse(idStr, out int id)) { await SendBadRequest(ctx, "Invalid ID").ConfigureAwait(false); return; }

                UrlRewrite? rewrite = await _Client.UrlRewrites.GetByIdAsync(id, ctx.Token).ConfigureAwait(false);
                if (rewrite == null) { await SendNotFound(ctx, "Rewrite not found").ConfigureAwait(false); return; }

                await SendOk(ctx, rewrite).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task UpdateRewriteAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot update resources").ConfigureAwait(false); return; }

                string? idStr = ctx.Request.Url.Parameters["id"];
                if (!Int32.TryParse(idStr, out int id)) { await SendBadRequest(ctx, "Invalid ID").ConfigureAwait(false); return; }

                UrlRewrite? rewrite = await ReadBody<UrlRewrite>(ctx).ConfigureAwait(false);
                if (rewrite == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                rewrite.Id = id;
                UrlRewrite updated = await _Client.UrlRewrites.UpdateAsync(rewrite, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "updated rewrite " + updated.Id);
                await SendOk(ctx, updated).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task DeleteRewriteAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot delete resources").ConfigureAwait(false); return; }

                string? idStr = ctx.Request.Url.Parameters["id"];
                if (!Int32.TryParse(idStr, out int id)) { await SendBadRequest(ctx, "Invalid ID").ConfigureAwait(false); return; }

                UrlRewrite? existing = await _Client.UrlRewrites.GetByIdAsync(id, ctx.Token).ConfigureAwait(false);
                if (existing == null) { await SendNotFound(ctx, "Rewrite not found").ConfigureAwait(false); return; }

                await _Client.UrlRewrites.DeleteByIdAsync(id, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "deleted rewrite " + existing.Id);
                await SendNoContent(ctx).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        #endregion

        #region Private-Methods-BlockedHeaders

        private async Task GetBlockedHeadersAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                int skip = Int32.TryParse(ctx.Request.Query.Elements["skip"], out int skipVal) ? skipVal : 0;
                int? take = Int32.TryParse(ctx.Request.Query.Elements["take"], out int takeVal) ? takeVal : (int?)null;

                List<BlockedHeader> headers = await _Client.BlockedHeaders.GetAllAsync(skip, take, ctx.Token).ConfigureAwait(false);
                await SendOk(ctx, headers).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task CreateBlockedHeaderAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot create resources").ConfigureAwait(false); return; }

                BlockedHeader? header = await ReadBody<BlockedHeader>(ctx).ConfigureAwait(false);
                if (header == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                BlockedHeader created = await _Client.BlockedHeaders.CreateAsync(header, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "created blocked header " + created.Id);
                await SendCreated(ctx, created).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task GetBlockedHeaderAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? idStr = ctx.Request.Url.Parameters["id"];
                if (!Int32.TryParse(idStr, out int id)) { await SendBadRequest(ctx, "Invalid ID").ConfigureAwait(false); return; }

                BlockedHeader? header = await _Client.BlockedHeaders.GetByIdAsync(id, ctx.Token).ConfigureAwait(false);
                if (header == null) { await SendNotFound(ctx, "Blocked header not found").ConfigureAwait(false); return; }

                await SendOk(ctx, header).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task DeleteBlockedHeaderAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot delete resources").ConfigureAwait(false); return; }

                string? idStr = ctx.Request.Url.Parameters["id"];
                if (!Int32.TryParse(idStr, out int id)) { await SendBadRequest(ctx, "Invalid ID").ConfigureAwait(false); return; }

                BlockedHeader? existing = await _Client.BlockedHeaders.GetByIdAsync(id, ctx.Token).ConfigureAwait(false);
                if (existing == null) { await SendNotFound(ctx, "Blocked header not found").ConfigureAwait(false); return; }

                await _Client.BlockedHeaders.DeleteByIdAsync(id, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "deleted blocked header " + existing.Id);
                await SendNoContent(ctx).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        #endregion

        #region Private-Methods-Users

        private async Task GetUsersAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? searchTerm = ctx.Request.Query.Elements["search"];
                int skip = Int32.TryParse(ctx.Request.Query.Elements["skip"], out int skipVal) ? skipVal : 0;
                int? take = Int32.TryParse(ctx.Request.Query.Elements["take"], out int takeVal) ? takeVal : (int?)null;

                List<UserMaster> users = await _Client.Users.GetAllAsync(searchTerm, skip, take, ctx.Token).ConfigureAwait(false);
                await SendOk(ctx, users).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task CreateUserAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot create resources").ConfigureAwait(false); return; }

                UserMaster? user = await ReadBody<UserMaster>(ctx).ConfigureAwait(false);
                if (user == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                UserMaster created = await _Client.Users.CreateAsync(user, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "created user " + created.Username);
                await SendCreated(ctx, created).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task GetUserAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                UserMaster? user = await _Client.Users.GetByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                if (user == null) { await SendNotFound(ctx, "User not found").ConfigureAwait(false); return; }

                await SendOk(ctx, user).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task UpdateUserAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot update resources").ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                UserMaster? user = await ReadBody<UserMaster>(ctx).ConfigureAwait(false);
                if (user == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                user.GUID = guid;
                UserMaster updated = await _Client.Users.UpdateAsync(user, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "updated user " + updated.Username);
                await SendOk(ctx, updated).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task DeleteUserAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot delete resources").ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                UserMaster? existing = await _Client.Users.GetByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                if (existing == null) { await SendNotFound(ctx, "User not found").ConfigureAwait(false); return; }

                await _Client.Users.DeleteByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "deleted user " + existing.Username);
                await SendNoContent(ctx).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        #endregion

        #region Private-Methods-Credentials

        private async Task GetCredentialsAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? searchTerm = ctx.Request.Query.Elements["search"];
                int skip = Int32.TryParse(ctx.Request.Query.Elements["skip"], out int skipVal) ? skipVal : 0;
                int? take = Int32.TryParse(ctx.Request.Query.Elements["take"], out int takeVal) ? takeVal : (int?)null;

                List<Credential> credentials = await _Client.Credentials.GetAllAsync(searchTerm, skip, take, ctx.Token).ConfigureAwait(false);
                await SendOk(ctx, credentials).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task CreateCredentialAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot create resources").ConfigureAwait(false); return; }

                Credential? credential = await ReadBody<Credential>(ctx).ConfigureAwait(false);
                if (credential == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                Credential created = await _Client.Credentials.CreateAsync(credential, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "created credential " + created.GUID);
                await SendCreated(ctx, created).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task GetCredentialAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                Credential? credential = await _Client.Credentials.GetByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                if (credential == null) { await SendNotFound(ctx, "Credential not found").ConfigureAwait(false); return; }

                await SendOk(ctx, credential).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task UpdateCredentialAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot update resources").ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                Credential? existingCredential = await _Client.Credentials.GetByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                if (existingCredential == null) { await SendNotFound(ctx, "Credential not found").ConfigureAwait(false); return; }

                Credential? credential = await ReadBody<Credential>(ctx).ConfigureAwait(false);
                if (credential == null) { await SendBadRequest(ctx, "Invalid request body").ConfigureAwait(false); return; }

                credential.GUID = guid;
                Credential updated = await _Client.Credentials.UpdateAsync(credential, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "updated credential " + updated.GUID);
                await SendOk(ctx, updated).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task DeleteCredentialAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot delete resources").ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                Credential? credential = await _Client.Credentials.GetByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                if (credential == null) { await SendNotFound(ctx, "Credential not found").ConfigureAwait(false); return; }

                await _Client.Credentials.DeleteByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "deleted credential " + credential.GUID);
                await SendNoContent(ctx).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task RegenerateCredentialAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot update resources").ConfigureAwait(false); return; }

                string? guidStr = ctx.Request.Url.Parameters["guid"];
                if (!Guid.TryParse(guidStr, out Guid guid)) { await SendBadRequest(ctx, "Invalid GUID").ConfigureAwait(false); return; }

                Credential? credential = await _Client.Credentials.GetByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                if (credential == null) { await SendNotFound(ctx, "Credential not found").ConfigureAwait(false); return; }

                Credential updated = await _Client.Credentials.RegenerateBearerTokenAsync(guid, ctx.Token).ConfigureAwait(false);
                _Logging.Info(_Header + "regenerated credential " + updated.GUID);
                await SendOk(ctx, updated).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        #endregion

        #region Private-Methods-RequestHistory

        private async Task GetRequestHistoryAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                int skip = Int32.TryParse(ctx.Request.Query.Elements["skip"], out int skipVal) ? skipVal : 0;
                int? take = Int32.TryParse(ctx.Request.Query.Elements["take"], out int takeVal) ? takeVal : (int?)null;

                // Check for filter parameters
                string? startStr = ctx.Request.Query.Elements["start"];
                string? endStr = ctx.Request.Query.Elements["end"];
                string? endpointGuidStr = ctx.Request.Query.Elements["endpoint"];
                string? originGuidStr = ctx.Request.Query.Elements["origin"];

                List<Models.RequestHistory> history;

                if (!String.IsNullOrEmpty(startStr) && !String.IsNullOrEmpty(endStr))
                {
                    if (DateTime.TryParse(startStr, out DateTime start) && DateTime.TryParse(endStr, out DateTime end))
                    {
                        history = await _Client.RequestHistory.GetByTimeRangeAsync(start, end, skip, take, ctx.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await SendBadRequest(ctx, "Invalid date format").ConfigureAwait(false);
                        return;
                    }
                }
                else if (!String.IsNullOrEmpty(endpointGuidStr) && Guid.TryParse(endpointGuidStr, out Guid endpointGuid))
                {
                    history = await _Client.RequestHistory.GetByEndpointGuidAsync(endpointGuid, skip, take, ctx.Token).ConfigureAwait(false);
                }
                else if (!String.IsNullOrEmpty(originGuidStr) && Guid.TryParse(originGuidStr, out Guid originGuid))
                {
                    history = await _Client.RequestHistory.GetByOriginGuidAsync(originGuid, skip, take, ctx.Token).ConfigureAwait(false);
                }
                else
                {
                    history = await _Client.RequestHistory.GetAllAsync(skip, take, ctx.Token).ConfigureAwait(false);
                }

                await SendOk(ctx, history).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task GetRecentHistoryAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                int count = Int32.TryParse(ctx.Request.Query.Elements["count"], out int countVal) ? countVal : 100;
                if (count < 1) count = 1;
                if (count > 1000) count = 1000;

                List<Models.RequestHistory> history = await _Client.RequestHistory.GetRecentAsync(count, ctx.Token).ConfigureAwait(false);
                await SendOk(ctx, history).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task GetFailedHistoryAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                int skip = Int32.TryParse(ctx.Request.Query.Elements["skip"], out int skipVal) ? skipVal : 0;
                int? take = Int32.TryParse(ctx.Request.Query.Elements["take"], out int takeVal) ? takeVal : (int?)null;

                List<Models.RequestHistory> history = await _Client.RequestHistory.GetFailedRequestsAsync(skip, take, ctx.Token).ConfigureAwait(false);
                await SendOk(ctx, history).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task GetHistoryByIdAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                string? idStr = ctx.Request.Url.Parameters["id"];
                Models.RequestHistory? history = null;

                // Try GUID first, then numeric ID
                if (Guid.TryParse(idStr, out Guid guid))
                {
                    history = await _Client.RequestHistory.GetByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                }
                else if (Int64.TryParse(idStr, out long id))
                {
                    history = await _Client.RequestHistory.GetByIdAsync(id, ctx.Token).ConfigureAwait(false);
                }
                else
                {
                    await SendBadRequest(ctx, "Invalid ID or GUID").ConfigureAwait(false);
                    return;
                }

                if (history == null) { await SendNotFound(ctx, "History record not found").ConfigureAwait(false); return; }

                await SendOk(ctx, history).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task DeleteHistoryAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot delete resources").ConfigureAwait(false); return; }

                string? idStr = ctx.Request.Url.Parameters["id"];

                // Try GUID first, then numeric ID
                if (Guid.TryParse(idStr, out Guid guid))
                {
                    await _Client.RequestHistory.DeleteByGuidAsync(guid, ctx.Token).ConfigureAwait(false);
                }
                else if (Int64.TryParse(idStr, out long id))
                {
                    await _Client.RequestHistory.DeleteByIdAsync(id, ctx.Token).ConfigureAwait(false);
                }
                else
                {
                    await SendBadRequest(ctx, "Invalid ID or GUID").ConfigureAwait(false);
                    return;
                }

                await SendNoContent(ctx).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task RunHistoryCleanupAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }
                if (!HasWriteAccess(ctx)) { await SendForbidden(ctx, "Read-only credentials cannot delete resources").ConfigureAwait(false); return; }

                // Get optional days parameter
                int days = Int32.TryParse(ctx.Request.Query.Elements["days"], out int daysVal) ? daysVal : 0;

                int deleted = 0;
                if (days > 0)
                {
                    DateTime cutoff = DateTime.UtcNow.AddDays(-days);
                    deleted = await _Client.RequestHistory.DeleteOlderThanAsync(cutoff, ctx.Token).ConfigureAwait(false);
                }

                await SendOk(ctx, new { deletedRecords = deleted }).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        private async Task GetHistoryStatsAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                long totalCount = await _Client.RequestHistory.CountAsync(ctx.Token).ConfigureAwait(false);
                long failedCount = await _Client.RequestHistory.CountFailedAsync(ctx.Token).ConfigureAwait(false);

                await SendOk(ctx, new
                {
                    totalRequests = totalCount,
                    failedRequests = failedCount,
                    successRate = totalCount > 0 ? Math.Round((double)(totalCount - failedCount) / totalCount * 100, 2) : 0
                }).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        #endregion

        #region Private-Methods-Health

        private async Task GetHealthAsync(HttpContextBase ctx)
        {
            try
            {
                if (!AuthenticateRequest(ctx)) { await SendUnauthorized(ctx).ConfigureAwait(false); return; }

                await SendOk(ctx, new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow.ToString("o"),
                    version = "4.0.2"
                }).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        #endregion

        #region Private-Methods-OpenApi

        private async Task GetManagementOpenApiAsync(HttpContextBase ctx)
        {
            try
            {
                // OpenAPI document does not require authentication for discoverability
                string? document = _CachedOpenApiDocument;

                if (String.IsNullOrEmpty(document))
                {
                    // Try to regenerate if cache is empty
                    try
                    {
                        SwitchboardOpenApiDocumentGenerator generator = new SwitchboardOpenApiDocumentGenerator();
                        document = generator.GenerateManagementApiDocument(_Settings.BasePath, _ServerUrl);
                        _CachedOpenApiDocument = document;
                    }
                    catch (Exception ex)
                    {
                        _Logging.Warn(_Header + "failed to generate Management API OpenAPI document: " + ex.Message);
                        ctx.Response.StatusCode = 500;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.Send("{\"error\":\"Failed to generate OpenAPI document\"}").ConfigureAwait(false);
                        return;
                    }
                }

                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                ctx.Response.Headers.Add("Pragma", "no-cache");
                ctx.Response.Headers.Add("Expires", "0");

                await ctx.Response.Send(document).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "exception in Management OpenAPI handler: " + ex.Message);
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"Internal server error\"}").ConfigureAwait(false);
            }
        }

        private async Task GetManagementSwaggerUiAsync(HttpContextBase ctx)
        {
            try
            {
                // Swagger UI does not require authentication for discoverability
                string? html = _CachedSwaggerHtml;

                if (String.IsNullOrEmpty(html))
                {
                    html = SwaggerUiHandler.GenerateHtml("/openapi.json", "Switchboard Management API");
                    _CachedSwaggerHtml = html;
                }

                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                ctx.Response.Headers.Add("Pragma", "no-cache");
                ctx.Response.Headers.Add("Expires", "0");

                await ctx.Response.Send(html).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "exception in Management Swagger UI handler: " + ex.Message);
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("<html><body><h1>Error</h1><p>Failed to load Swagger UI</p></body></html>").ConfigureAwait(false);
            }
        }

        #endregion

        #region Private-Methods-CurrentUser

        private async Task GetCurrentUserAsync(HttpContextBase ctx)
        {
            try
            {
                // Get token from Authorization header
                string? authHeader = ctx.Request.Headers.Get("Authorization");
                if (String.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    await SendUnauthorized(ctx).ConfigureAwait(false);
                    return;
                }

                string token = authHeader.Substring(7).Trim();
                if (String.IsNullOrWhiteSpace(token))
                {
                    await SendUnauthorized(ctx).ConfigureAwait(false);
                    return;
                }

                // Check if this is the admin token
                if (!String.IsNullOrWhiteSpace(_Settings.AdminToken) && token == _Settings.AdminToken)
                {
                    // Return admin user info
                    await SendOk(ctx, new
                    {
                        guid = Guid.Empty.ToString(),
                        username = "admin",
                        email = (string?)null,
                        firstName = "Admin",
                        lastName = "User",
                        isAdmin = true,
                        active = true
                    }).ConfigureAwait(false);
                    return;
                }

                // Look up the user by bearer token
                UserMaster? user = await _Client.Credentials.GetUserByBearerTokenAsync(token, ctx.Token).ConfigureAwait(false);

                if (user == null || !user.Active)
                {
                    await SendUnauthorized(ctx).ConfigureAwait(false);
                    return;
                }

                // Return user info (excluding sensitive data)
                await SendOk(ctx, new
                {
                    guid = user.GUID.ToString(),
                    username = user.Username,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    isAdmin = user.IsAdmin,
                    active = user.Active
                }).ConfigureAwait(false);
            }
            catch (Exception ex) { await SendError(ctx, ex).ConfigureAwait(false); }
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose pattern.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                _Disposed = true;
            }
        }

        #endregion
    }
}
