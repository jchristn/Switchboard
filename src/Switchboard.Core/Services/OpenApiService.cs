namespace Switchboard.Core.Services
{
    using System;
    using System.Threading.Tasks;
    using SyslogLogging;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using Switchboard.Core.Settings;

    /// <summary>
    /// Service for generating and serving OpenAPI documentation.
    /// </summary>
    public class OpenApiService : IDisposable
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

        private readonly string _Header = "[OpenApiService] ";
        private readonly SwitchboardSettings _Settings;
        private LoggingModule _Logging;
        private readonly SwitchboardOpenApiDocumentGenerator _Generator;
        private string _CachedDocument = null;
        private string _CachedSwaggerHtml = null;
        private bool _IsDisposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Switchboard settings.</param>
        /// <param name="logging">Logging module.</param>
        public OpenApiService(SwitchboardSettings settings, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Generator = new SwitchboardOpenApiDocumentGenerator();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Initialize OpenAPI routes on the webserver.
        /// </summary>
        /// <param name="webserver">Watson webserver instance.</param>
        public void InitializeRoutes(Webserver webserver)
        {
            if (webserver == null) throw new ArgumentNullException(nameof(webserver));

            if (_Settings.OpenApi == null || !_Settings.OpenApi.Enable)
            {
                _Logging.Debug(_Header + "OpenAPI is disabled, skipping route registration");
                return;
            }

            _Logging.Info(_Header + "initializing OpenAPI routes");

            // Pre-generate the OpenAPI document
            try
            {
                _CachedDocument = _Generator.Generate(_Settings);
                _Logging.Debug(_Header + "OpenAPI document generated successfully");
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to generate OpenAPI document: " + ex.Message);
                _CachedDocument = null;
            }

            // Pre-generate Swagger UI HTML
            _CachedSwaggerHtml = SwaggerUiHandler.GenerateHtml(
                _Settings.OpenApi.DocumentPath,
                _Settings.OpenApi.Title);

            // Register OpenAPI JSON endpoint
            string documentPath = _Settings.OpenApi.DocumentPath ?? "/openapi.json";
            webserver.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                documentPath,
                OpenApiDocumentHandler);

            _Logging.Info(_Header + "OpenAPI document available at " + documentPath);

            // Register Swagger UI endpoint if enabled
            if (_Settings.OpenApi.EnableSwaggerUi)
            {
                string swaggerPath = _Settings.OpenApi.SwaggerUiPath ?? "/swagger";
                webserver.Routes.PreAuthentication.Static.Add(
                    HttpMethod.GET,
                    swaggerPath,
                    SwaggerUiRouteHandler);

                _Logging.Info(_Header + "Swagger UI available at " + swaggerPath);
            }
        }

        /// <summary>
        /// Regenerate the OpenAPI document cache.
        /// Call this if settings have changed at runtime.
        /// </summary>
        public void RegenerateDocument()
        {
            try
            {
                _CachedDocument = _Generator.Generate(_Settings);
                _Logging.Debug(_Header + "OpenAPI document regenerated successfully");
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to regenerate OpenAPI document: " + ex.Message);
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
                    _CachedDocument = null;
                    _CachedSwaggerHtml = null;
                }

                _IsDisposed = true;
            }
        }

        private async Task OpenApiDocumentHandler(HttpContextBase ctx)
        {
            try
            {
                string document = _CachedDocument;

                if (String.IsNullOrEmpty(document))
                {
                    // Try to regenerate if cache is empty
                    try
                    {
                        document = _Generator.Generate(_Settings);
                        _CachedDocument = document;
                    }
                    catch (Exception ex)
                    {
                        _Logging.Warn(_Header + "failed to generate OpenAPI document on request: " + ex.Message);
                        ctx.Response.StatusCode = 500;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.Send("{\"error\":\"Failed to generate OpenAPI document\"}", ctx.Token).ConfigureAwait(false);
                        return;
                    }
                }

                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                ctx.Response.Headers.Add("Pragma", "no-cache");
                ctx.Response.Headers.Add("Expires", "0");

                await ctx.Response.Send(document, ctx.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "exception in OpenAPI document handler: " + ex.Message);
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"Internal server error\"}", ctx.Token).ConfigureAwait(false);
            }
        }

        private async Task SwaggerUiRouteHandler(HttpContextBase ctx)
        {
            try
            {
                string html = _CachedSwaggerHtml;

                if (String.IsNullOrEmpty(html))
                {
                    html = SwaggerUiHandler.GenerateHtml(
                        _Settings.OpenApi.DocumentPath,
                        _Settings.OpenApi.Title);
                    _CachedSwaggerHtml = html;
                }

                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                ctx.Response.Headers.Add("Pragma", "no-cache");
                ctx.Response.Headers.Add("Expires", "0");

                await ctx.Response.Send(html, ctx.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "exception in Swagger UI handler: " + ex.Message);
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("<html><body><h1>Error</h1><p>Failed to load Swagger UI</p></body></html>", ctx.Token).ConfigureAwait(false);
            }
        }

        #endregion
    }
}
