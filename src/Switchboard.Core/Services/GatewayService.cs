namespace Switchboard.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using RestWrapper;
    using SerializationHelper;
    using Switchboard.Core;
    using SyslogLogging;
    using Timestamps;
    using UrlMatcher;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Gateway service.
    /// </summary>
    public class GatewayService : IDisposable
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        #endregion

        #region Private-Members

        private readonly string _Header = "[Gateway] ";
        private SwitchboardSettings _Settings = null;
        private LoggingModule _Logging = null;
        private Serializer _Serializer = null;
        private Random _Random = new Random(Guid.NewGuid().GetHashCode());
        private bool _IsDisposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public GatewayService(
            SwitchboardSettings settings,
            LoggingModule logging,
            Serializer serializer)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
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

        /// <summary>
        /// Initialize routes.
        /// </summary>
        /// <param name="webserver">Webserver.</param>
        public void InitializeRoutes(WebserverBase webserver)
        {
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/", GetRootRoute);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.HEAD, "/", HeadRootRoute);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/favicon.ico", GetFaviconRoute);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.HEAD, "/favicon.ico", HeadFaviconRoute);
        }

        /// <summary>
        /// Route for handling OPTIONS requests.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        public async Task OptionsRoute(HttpContextBase ctx)
        {
            NameValueCollection responseHeaders = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

            string[] requestedHeaders = null;
            string headers = "";

            if (ctx.Request.Headers != null)
            {
                for (int i = 0; i < ctx.Request.Headers.Count; i++)
                {
                    string key = ctx.Request.Headers.GetKey(i);
                    string value = ctx.Request.Headers.Get(i);
                    if (String.IsNullOrEmpty(key)) continue;
                    if (String.IsNullOrEmpty(value)) continue;
                    if (String.Compare(key.ToLower(), "access-control-request-headers") == 0)
                    {
                        requestedHeaders = value.Split(',');
                        break;
                    }
                }
            }

            if (requestedHeaders != null)
            {
                foreach (string curr in requestedHeaders)
                {
                    headers += ", " + curr;
                }
            }

            responseHeaders.Add("Access-Control-Allow-Methods", "OPTIONS, HEAD, GET, PUT, POST, DELETE");
            responseHeaders.Add("Access-Control-Allow-Headers", "*, Content-Type, X-Requested-With, " + headers);
            responseHeaders.Add("Access-Control-Expose-Headers", "Content-Type, X-Requested-With, " + headers);
            responseHeaders.Add("Access-Control-Allow-Origin", "*");
            responseHeaders.Add("Accept", "*/*");
            responseHeaders.Add("Accept-Language", "en-US, en");
            responseHeaders.Add("Accept-Charset", "ISO-8859-1, utf-8");
            responseHeaders.Add("Connection", "keep-alive");

            ctx.Response.StatusCode = 200;
            ctx.Response.Headers = responseHeaders;
            await ctx.Response.Send();
            return;
        }

        /// <summary>
        /// Pre-routing handler.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        public async Task PreRoutingHandler(HttpContextBase ctx)
        {
        }

        /// <summary>
        /// Post-routing handler.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        public async Task PostRoutingHandler(HttpContextBase ctx)
        {
        }

        /// <summary>
        /// Authenticate request.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        public async Task AuthenticateRequest(HttpContextBase ctx)
        {
        }

        /// <summary>
        /// Default request handler.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        public async Task DefaultRoute(HttpContextBase ctx)
        {
            Guid requestGuid = Guid.NewGuid();

            MatchingApiEndpoint match = FindApiEndpoint(ctx);
            if (match == null)
            {
                _Logging.Warn(_Header + "no API endpoint found for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = Constants.JsonContentType;
                await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.BadRequest, null, "No matching API endpoint found"), true));
                return;
            }

            try
            {
                OriginServer origin = FindOriginServer(match.Endpoint);
                if (origin == null)
                {
                    _Logging.Warn(_Header + "no origin server found for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                    ctx.Response.StatusCode = 502;
                    ctx.Response.ContentType = Constants.JsonContentType;
                    await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.BadGateway, null, "No origin servers are available to service your request"), true));
                    return;
                }

                if (match.Endpoint.MaxRequestBodySize > 0 && ctx.Request.ContentLength > match.Endpoint.MaxRequestBodySize)
                {
                    _Logging.Warn(_Header + "request too large from " + ctx.Request.Source.IpAddress + ": " + ctx.Request.ContentLength + " bytes");
                    ctx.Response.StatusCode = 400;
                    ctx.Response.ContentType = Constants.JsonContentType;
                    await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.TooLarge, null, "Your request was too large"), true));
                    return;
                }

                bool responseReceived = await EmitRequest(
                    requestGuid,
                    match,
                    origin,
                    ctx);

                if (!responseReceived)
                {
                    _Logging.Warn(_Header + "no response or exception from " + origin.Identifier + " for API endpoint " + match.Endpoint.Identifier);
                    ctx.Response.StatusCode = 502;
                    ctx.Response.ContentType = Constants.JsonContentType;
                    await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.BadGateway), true));
                    return;
                }
            }
            catch (Exception e)
            {
                _Logging.Warn(_Header + "exception:" + Environment.NewLine + e.ToString());
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send();
            }
        }

        #endregion

        #region Private-Methods

        private async Task GetRootRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = Constants.HtmlContentType;
            await ctx.Response.Send(Constants.HtmlHomepage);
        }

        private async Task HeadRootRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = Constants.TextContentType;
            await ctx.Response.Send();
        }

        private async Task GetFaviconRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = Constants.FaviconContentType;
            await ctx.Response.Send(File.ReadAllBytes(Constants.FaviconFilename));
        }

        private async Task HeadFaviconRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = Constants.FaviconContentType;
            await ctx.Response.Send();
        }

        private MatchingApiEndpoint FindApiEndpoint(HttpContextBase ctx)
        {
            NameValueCollection nvc = null;

            Matcher matcher = new Matcher(ctx.Request.Url.RawWithoutQuery);

            foreach (ApiEndpoint ep in _Settings.Endpoints)
            {
                if (ep.ParameterizedUrls != null && ep.ParameterizedUrls.Count > 0)
                {
                    if (ep.ParameterizedUrls.Keys.Any(k => k.Equals(ctx.Request.Method.ToString()))
                        && ep.ParameterizedUrls.Values != null 
                        && ep.ParameterizedUrls.Values.Count > 0)
                    {
                        KeyValuePair<string, List<string>> match = ep.ParameterizedUrls.First(k => k.Key.Equals(ctx.Request.Method.ToString()));
                        foreach (string url in match.Value)
                        {
                            if (matcher.Match(url, out nvc))
                            {
                                return new MatchingApiEndpoint
                                {
                                    Endpoint = ep,
                                    ParameterizedUrl = url,
                                    Parameters = nvc
                                };
                            }
                        }
                    }
                }
            }

            return null;
        }

        private OriginServer FindOriginServer(ApiEndpoint endpoint)
        {
            if (endpoint == null) return null;
            if (endpoint.OriginServers == null || endpoint.OriginServers.Count < 1) return null;

            string originIdentifier = null;
            OriginServer origin = null;

            lock (endpoint)
            {
                if (endpoint.LoadBalancing == LoadBalancingMode.Random)
                {
                    int index = _Random.Next(0, endpoint.OriginServers.Count);
                    endpoint.LastIndex = index;
                    originIdentifier = endpoint.OriginServers[index];
                    origin = _Settings.Origins.FirstOrDefault(o => o.Identifier.Equals(originIdentifier));
                    if (origin != default(OriginServer)) return origin;
                    return null;
                }
                else if (endpoint.LoadBalancing == LoadBalancingMode.RoundRobin)
                {
                    if (endpoint.LastIndex >= endpoint.OriginServers.Count) endpoint.LastIndex = 0;
                    originIdentifier = endpoint.OriginServers[endpoint.LastIndex];

                    if ((endpoint.LastIndex + 1) > (endpoint.OriginServers.Count - 1)) endpoint.LastIndex = 0;
                    else endpoint.LastIndex = endpoint.LastIndex + 1;

                    origin = _Settings.Origins.FirstOrDefault(o => o.Identifier.Equals(originIdentifier));
                    if (origin != default(OriginServer)) return origin;
                    return null;
                }
                else
                {
                    throw new ArgumentException("Unknown load balancing scheme '" + endpoint.LoadBalancing.ToString() + "'.");
                }
            }
        }

        private System.Net.Http.HttpMethod ConvertHttpMethod(WatsonWebserver.Core.HttpMethod method)
        {
            switch (method)
            {
                case HttpMethod.CONNECT:
                    return System.Net.Http.HttpMethod.Connect;
                case HttpMethod.DELETE:
                    return System.Net.Http.HttpMethod.Delete;
                case HttpMethod.GET:
                    return System.Net.Http.HttpMethod.Get;
                case HttpMethod.HEAD:
                    return System.Net.Http.HttpMethod.Head;
                case HttpMethod.OPTIONS:
                    return System.Net.Http.HttpMethod.Options;
                case HttpMethod.PATCH:
                    return System.Net.Http.HttpMethod.Patch;
                case HttpMethod.POST:
                    return System.Net.Http.HttpMethod.Post;
                case HttpMethod.PUT:
                    return System.Net.Http.HttpMethod.Put;
                case HttpMethod.TRACE:
                    return System.Net.Http.HttpMethod.Trace;
                default:
                    throw new ArgumentException("Unknown HTTP method " + method.ToString());
            }
        }

        private async Task<bool> EmitRequest(
            Guid requestGuid, 
            MatchingApiEndpoint endpoint, 
            OriginServer origin, 
            HttpContextBase ctx)
        {
            _Logging.Debug(_Header + "emitting request to " + origin.Identifier + " for API endpoint " + endpoint.Endpoint.Identifier + " for request " + requestGuid.ToString());

            int statusCode = 0;
            
            using (Timestamp ts = new Timestamp())
            {
                try
                {
                    using (RestRequest req = new RestRequest(
                        origin.UrlPrefix + ctx.Request.Url.RawWithQuery,
                        ConvertHttpMethod(ctx.Request.Method)))
                    {
                        #region Set-Timeout

                        if (endpoint.Endpoint.TimeoutMs > 0)
                            req.TimeoutMilliseconds = endpoint.Endpoint.TimeoutMs;

                        #endregion

                        #region Add-Proxy-Headers

                        req.Headers.Add(Constants.ForwardedForHeader, ctx.Request.Source.IpAddress);
                        req.Headers.Add(Constants.RequestIdHeader, requestGuid.ToString());

                        #endregion

                        #region Add-Original-Headers

                        if (ctx.Request.Headers != null && ctx.Request.Headers.Count > 0)
                        {
                            foreach (string key in ctx.Request.Headers.Keys)
                            {
                                // global blocked headers
                                if (_Settings.BlockedHeaders.Any(h => h.Equals(key.ToLower()))) continue;

                                // local blocked headers
                                if (endpoint.Endpoint.BlockedHeaders != null && endpoint.Endpoint.BlockedHeaders.Any(h => h.Equals(key))) continue;

                                string val = ctx.Request.Headers.Get(key);
                                req.Headers.Add(key, val);
                            }
                        }

                        #endregion

                        #region Replace-Host-Header

                        foreach (string key in req.Headers.AllKeys)
                        {
                            if (key.ToLower().Equals("host"))
                            {
                                req.Headers.Remove(key);
                                req.Headers.Add("Host", origin.Hostname + ":" + origin.Port.ToString());
                            }
                        }

                        #endregion

                        #region Send-Request

                        if (ctx.Request.DataAsBytes != null && ctx.Request.DataAsBytes.Length > 0)
                        {
                            #region With-Data

                            if (endpoint.Endpoint.LogRequestBody || origin.LogRequestBody)
                            {
                                _Logging.Debug(
                                    _Header
                                    + "request body (" + ctx.Request.DataAsBytes.Length + " bytes): "
                                    + Environment.NewLine
                                    + Encoding.UTF8.GetString(ctx.Request.DataAsBytes));
                            }

                            if (String.IsNullOrEmpty(ctx.Request.ContentType))
                                req.ContentType = ctx.Request.ContentType;
                            else
                                req.ContentType = Constants.BinaryContentType;

                            using (RestResponse resp = await req.SendAsync(ctx.Request.DataAsBytes))
                            {
                                if (resp != null)
                                {
                                    foreach (string key in resp.Headers)
                                    {
                                        // global blocked headers
                                        if (_Settings.BlockedHeaders.Any(h => h.Equals(key.ToLower()))) continue;

                                        // local blocked headers
                                        if (endpoint.Endpoint.BlockedHeaders != null && endpoint.Endpoint.BlockedHeaders.Any(h => h.Equals(key))) continue;

                                        string val = resp.Headers.Get(key);
                                        ctx.Response.Headers.Add(key, val);
                                    }

                                    if (endpoint.Endpoint.LogResponseBody || origin.LogResponseBody)
                                    {
                                        if (resp.DataAsBytes != null && resp.DataAsBytes.Length > 0)
                                        {
                                            _Logging.Debug(
                                                _Header
                                                + "response body (" + resp.DataAsBytes.Length + " bytes) status " + resp.StatusCode + ": "
                                                + Environment.NewLine
                                                + Encoding.UTF8.GetString(resp.DataAsBytes));
                                        }
                                        else
                                        {
                                            _Logging.Debug(
                                                _Header
                                                + "response body (0 bytes) status " + resp.StatusCode);
                                        }
                                    }

                                    statusCode = resp.StatusCode;
                                    ctx.Response.StatusCode = resp.StatusCode;
                                    ctx.Response.ContentType = resp.ContentType;
                                    await ctx.Response.Send(resp.DataAsBytes);
                                    return true;
                                }
                                else
                                {
                                    return false;
                                }
                            }

                            #endregion
                        }
                        else
                        {
                            #region Without-Data

                            if (endpoint.Endpoint.LogRequestBody || origin.LogRequestBody)
                                _Logging.Debug(_Header + "request body (0 bytes)");

                            using (RestResponse resp = await req.SendAsync())
                            {
                                if (resp != null)
                                {
                                    foreach (string key in resp.Headers)
                                    {
                                        // global blocked headers
                                        if (_Settings.BlockedHeaders.Any(h => h.Equals(key.ToLower()))) continue;

                                        // local blocked headers
                                        if (endpoint.Endpoint.BlockedHeaders != null && endpoint.Endpoint.BlockedHeaders.Any(h => h.Equals(key))) continue;

                                        string val = resp.Headers.Get(key);
                                        ctx.Response.Headers.Add(key, val);
                                    }

                                    if (endpoint.Endpoint.LogResponseBody || origin.LogResponseBody)
                                    {
                                        if (resp.DataAsBytes != null && resp.DataAsBytes.Length > 0)
                                        {
                                            _Logging.Debug(
                                                _Header
                                                + "response body (" + resp.DataAsBytes.Length + " bytes) status " + resp.StatusCode + ": "
                                                + Environment.NewLine
                                                + Encoding.UTF8.GetString(resp.DataAsBytes));
                                        }
                                        else
                                        {
                                            _Logging.Debug(
                                                _Header
                                                + "response body (0 bytes) status " + resp.StatusCode);
                                        }
                                    }

                                    statusCode = resp.StatusCode;
                                    ctx.Response.StatusCode = resp.StatusCode;
                                    ctx.Response.ContentType = resp.ContentType;
                                    await ctx.Response.Send(resp.DataAsBytes);
                                    return true;
                                }
                                else
                                {
                                    return false;
                                }
                            }

                            #endregion
                        }

                        #endregion
                    }
                }
                catch (Exception e)
                {
                    _Logging.Warn(
                        _Header 
                        + "exception emitting request to " + origin.Identifier 
                        + " for API endpoint " + endpoint.Endpoint.Identifier 
                        + " for request " + requestGuid.ToString() 
                        + Environment.NewLine 
                        + e.ToString());

                    return false;
                }
                finally
                {
                    ts.End = DateTime.UtcNow;
                    _Logging.Debug(_Header + "completed request " + requestGuid.ToString() + " origin " + origin.Identifier + " endpoint " + endpoint.Endpoint.Identifier + " " + statusCode + " (" + ts.TotalMs + "ms)");
                }
            }
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
