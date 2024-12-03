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
    public class GatewayService
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

            try
            {
                MatchingApiEndpoint match = FindApiEndpoint(ctx);
                if (match == null)
                {
                    _Logging.Warn(_Header + "no API endpoint found for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                    ctx.Response.StatusCode = 400;
                    ctx.Response.ContentType = Constants.JsonContentType;
                    await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.BadRequest, null, "No matching API endpoint found"), true));
                    return;
                }

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

                await EmitRequest(
                    requestGuid,
                    match, 
                    origin, 
                    ctx);
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

            foreach (ApiEndpoint ep in _Settings.Endpoints)
            {
                if (ep.Method == ctx.Request.Method)
                {
                    if (!String.IsNullOrEmpty(ep.UrlPrefix))
                    {
                        if (!ctx.Request.Url.RawWithoutQuery.StartsWith(ep.UrlPrefix))
                        {
                            continue;
                        }
                    }

                    Matcher matcher = new Matcher(ep.ParameterizedUrl);
                    if (matcher.Match(ctx.Request.Url.RawWithoutQuery, out nvc))
                    {
                        return new MatchingApiEndpoint
                        {
                            Endpoint = ep,
                            Parameters = nvc
                        };
                    }
                }
            }

            return null;
        }

        private OriginServer FindOriginServer(ApiEndpoint endpoint)
        {
            if (endpoint == null) return null;
            if (endpoint.OriginServers == null || endpoint.OriginServers.Count < 1) return null;

            lock (endpoint)
            {
                int index = _Random.Next(0, endpoint.OriginServers.Count);
                endpoint.LastIndex = index;
                string originIdentifier = endpoint.OriginServers[index];
                OriginServer origin = _Settings.Origins.FirstOrDefault(o => o.Identifier.Equals(originIdentifier));
                if (origin != default(OriginServer)) return origin;
                return null;
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

        private async Task EmitRequest(
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
                        // origin.UrlPrefix + ctx.Request.Url.RawWithQuery,
                        "https://www.google.com",
                        ConvertHttpMethod(endpoint.Endpoint.Method)))
                    {
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

                        #region Send-Response

                        using (RestResponse resp = await req.SendAsync())
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

                            statusCode = resp.StatusCode;
                            ctx.Response.StatusCode = resp.StatusCode;
                            ctx.Response.ContentType = resp.ContentType;
                            await ctx.Response.Send(resp.DataAsBytes);
                        }

                        #endregion
                    }
                }
                catch (Exception e)
                {
                    _Logging.Warn(_Header + "exception:" + Environment.NewLine + e.ToString());
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = Constants.JsonContentType;
                    await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.InternalError), true));
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
