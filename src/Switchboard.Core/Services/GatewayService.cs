namespace Switchboard.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Text;
    using System.Threading;
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

        private readonly string _Header = "[GatewayService] ";
        private SwitchboardSettings _Settings = null;
        private SwitchboardCallbacks _Callbacks = null;
        private LoggingModule _Logging = null;
        private Serializer _Serializer = null;
        private Random _Random = new Random(Guid.NewGuid().GetHashCode());
        private bool _IsDisposed = false;

        private const int BUFFER_SIZE = 65536;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="callbacks">Callbacks.</param>
        /// <param name="logging">Logging.</param>
        /// <param name="serializer">Serializer.</param>
        public GatewayService(
            SwitchboardSettings settings,
            SwitchboardCallbacks callbacks,
            LoggingModule logging,
            Serializer serializer)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
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
            AuthContext authContext = null;

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

                if (match.Endpoint.LogRequestFull)
                    _Logging.Debug(_Header + "incoming request:" + Environment.NewLine + _Serializer.SerializeJson(ctx.Request, true));

                if (match.Endpoint.BlockHttp10 && ctx.Request.ProtocolVersion.Equals("HTTP/1.0"))
                {
                    _Logging.Debug(_Header + "denying HTTP/1.0 request due to API endpoint configuration");
                    ctx.Response.StatusCode = 505;
                    ctx.Response.ContentType = Constants.JsonContentType;
                    await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.UnsupportedHttpVersion), true));
                    return;
                }

                if (match.AuthRequired)
                {
                    if (_Callbacks == null || _Callbacks.AuthenticateAndAuthorize == null)
                    {
                        _Logging.Warn(_Header + "API endpoint " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " requires auth but no auth callback set");
                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = Constants.JsonContentType;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.AuthenticationFailed), true));
                        return;
                    }

                    authContext = await _Callbacks.AuthenticateAndAuthorize(ctx);
                    if (authContext.Authentication.Result != AuthenticationResultEnum.Success 
                        && authContext.Authorization.Result != AuthorizationResultEnum.Success)
                    {
                        _Logging.Warn(
                            _Header + 
                            "auth failure reported for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " " +
                            "(" + authContext.Authentication.Result + "/" + authContext.Authorization.Result + ")" +
                            ": " + authContext.FailureMessage);

                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = Constants.JsonContentType;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.AuthenticationFailed, null, authContext.FailureMessage), true));
                        return;
                    }
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

                int totalRequests =
                    Volatile.Read(ref origin.ActiveRequests) +
                    Volatile.Read(ref origin.PendingRequests);

                if (totalRequests > origin.RateLimitRequestsThreshold)
                {
                    _Logging.Warn(_Header + "too many active requests for origin " + origin.Identifier + ", sending 429 response to request from " + ctx.Request.Source.IpAddress);
                    ctx.Response.StatusCode = 429;
                    ctx.Response.ContentType = Constants.JsonContentType;
                    await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.SlowDown)));
                    return;
                }

                Interlocked.Increment(ref origin.PendingRequests);

                bool responseReceived = await ProxyRequest(
                    requestGuid,
                    ctx,
                    match,
                    origin,
                    authContext);

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
                #region Unauthenticated-Endpoints

                if (ep.Unauthenticated.ParameterizedUrls != null && ep.Unauthenticated.ParameterizedUrls.Count > 0)
                {
                    if (ep.Unauthenticated.ParameterizedUrls.Keys.Any(k => k.Equals(ctx.Request.Method.ToString()))
                        && ep.Unauthenticated.ParameterizedUrls.Values != null
                        && ep.Unauthenticated.ParameterizedUrls.Values.Count > 0)
                    {
                        KeyValuePair<string, List<string>> match = ep.Unauthenticated.ParameterizedUrls.First(k => k.Key.Equals(ctx.Request.Method.ToString()));
                        foreach (string url in match.Value)
                        {
                            if (matcher.Match(url, out nvc))
                            {
                                return new MatchingApiEndpoint
                                {
                                    AuthRequired = false,
                                    Endpoint = ep,
                                    ParameterizedUrl = url,
                                    Parameters = nvc
                                };
                            }
                        }
                    }
                }

                #endregion

                #region Authenticated-Endpoints

                if (ep.Authenticated.ParameterizedUrls != null && ep.Authenticated.ParameterizedUrls.Count > 0)
                {
                    if (ep.Authenticated.ParameterizedUrls.Keys.Any(k => k.Equals(ctx.Request.Method.ToString()))
                        && ep.Authenticated.ParameterizedUrls.Values != null
                        && ep.Authenticated.ParameterizedUrls.Values.Count > 0)
                    {
                        KeyValuePair<string, List<string>> match = ep.Authenticated.ParameterizedUrls.First(k => k.Key.Equals(ctx.Request.Method.ToString()));
                        foreach (string url in match.Value)
                        {
                            if (matcher.Match(url, out nvc))
                            {
                                return new MatchingApiEndpoint
                                {
                                    AuthRequired = true,
                                    Endpoint = ep,
                                    ParameterizedUrl = url,
                                    Parameters = nvc
                                };
                            }
                        }
                    }
                }

                #endregion
            }

            return null;
        }

        private OriginServer FindOriginServer(ApiEndpoint endpoint)
        {
            if (endpoint == null) return null;
            if (endpoint.OriginServers == null || endpoint.OriginServers.Count < 1) return null;

            OriginServer origin = null;

            lock (endpoint.Lock)
            {
                List<OriginServer> healthyOrigins = _Settings.Origins
                    .Where(b => endpoint.OriginServers.Contains(b.Identifier))
                    .Where(b =>
                    {
                        lock (b.Lock)
                        {
                            return b.Healthy;
                        }
                    })
                    .ToList();

                if (healthyOrigins.Count < 1)
                {
                    _Logging.Warn(_Header + "no healthy origins found for endpoint " + endpoint.Identifier);
                    return null;
                }
                else
                {
                    if (endpoint.LoadBalancing == LoadBalancingMode.Random)
                    {
                        int index = _Random.Next(0, healthyOrigins.Count);
                        endpoint.LastIndex = index;
                        origin = healthyOrigins[index];
                        if (origin != default(OriginServer)) return origin;
                        return null;
                    }
                    else if (endpoint.LoadBalancing == LoadBalancingMode.RoundRobin)
                    {
                        if (endpoint.LastIndex >= healthyOrigins.Count) endpoint.LastIndex = _Random.Next(0, healthyOrigins.Count);
                        origin = healthyOrigins[endpoint.LastIndex];

                        if ((endpoint.LastIndex + 1) > (endpoint.OriginServers.Count - 1)) endpoint.LastIndex = 0;
                        else endpoint.LastIndex = endpoint.LastIndex + 1;

                        if (origin != default(OriginServer)) return origin;
                        return null;
                    }
                    else
                    {
                        throw new ArgumentException("Unknown load balancing scheme '" + endpoint.LoadBalancing.ToString() + "'.");
                    }
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

        private async Task<bool> ProxyRequest(
            Guid requestGuid,
            HttpContextBase ctx,
            MatchingApiEndpoint endpoint,
            OriginServer origin,
            AuthContext authResult)
        {
            _Logging.Debug(_Header + "proxying request to " + origin.Identifier + " for endpoint " + endpoint.Endpoint.Identifier + " for request " + requestGuid.ToString());

            RestResponse resp = null;

            using (Timestamp ts = new Timestamp())
            {
                try
                {
                    #region Rewrite-URL

                    string url = UrlTools.RewriteUrl(
                        ctx.Request.Method.ToString(),
                        ctx.Request.Url.RawWithoutQuery,
                        endpoint.Endpoint);

                    if (ctx.Request.Query != null && !String.IsNullOrEmpty(ctx.Request.Query.Querystring))
                        url += "?" + ctx.Request.Query.Querystring;

                    url = origin.UrlPrefix + url;

                    #endregion

                    #region Enter-Semaphore

                    await origin.Semaphore.WaitAsync().ConfigureAwait(false);
                    Interlocked.Increment(ref origin.ActiveRequests);
                    Interlocked.Decrement(ref origin.PendingRequests);

                    #endregion

                    #region Build-Request-and-Send

                    using (RestRequest req = new RestRequest(url, ConvertHttpMethod(ctx.Request.Method)))
                    {
                        if (endpoint.Endpoint.TimeoutMs > 0) req.TimeoutMilliseconds = endpoint.Endpoint.TimeoutMs;

                        req.Headers.Add(Constants.ForwardedForHeader, ctx.Request.Source.IpAddress);
                        req.Headers.Add(Constants.RequestIdHeader, requestGuid.ToString());

                        if (ctx.Request.Headers != null && ctx.Request.Headers.Count > 0)
                        {
                            foreach (string key in ctx.Request.Headers.Keys)
                            {
                                if (!req.Headers.AllKeys.Contains(key))
                                {
                                    string val = ctx.Request.Headers.Get(key);
                                    req.Headers.Add(key, val);
                                }
                            }
                        }

                        foreach (string key in req.Headers.AllKeys)
                        {
                            if (key.ToLower().Equals("host"))
                            {
                                req.Headers.Remove(key);
                                req.Headers.Add("Host", origin.Hostname + ":" + origin.Port.ToString());
                            }
                        }

                        #region Log-Request-Body

                        if (endpoint.Endpoint.LogRequestBody || origin.LogRequestBody)
                        {
                            _Logging.Debug(
                                _Header
                                + "request body (" + ctx.Request.DataAsBytes.Length + " bytes): "
                                + Environment.NewLine
                                + Encoding.UTF8.GetString(ctx.Request.DataAsBytes));

                            _Logging.Debug(_Header + "using content-type: " + req.ContentType);
                        }

                        #endregion

                        #region Send-Request

                        if (ctx.Request.DataAsBytes != null && ctx.Request.DataAsBytes.Length > 0)
                        {
                            #region With-Data

                            if (!String.IsNullOrEmpty(ctx.Request.ContentType))
                                req.ContentType = ctx.Request.ContentType;
                            else
                                req.ContentType = Constants.BinaryContentType;

                            resp = await req.SendAsync(ctx.Request.DataAsBytes);

                            #endregion
                        }
                        else
                        {
                            #region Without-Data

                            resp = await req.SendAsync();

                            #endregion
                        }

                        if (resp != null)
                        {
                            #region Log-Response-Body

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

                            #endregion

                            #region Set-Headers

                            ctx.Response.StatusCode = resp.StatusCode;
                            ctx.Response.ContentType = resp.ContentType;
                            ctx.Response.Headers = resp.Headers;
                            ctx.Response.Headers.Add(Constants.OriginServerHeader, origin.Identifier);
                            ctx.Response.Headers.Add(Constants.RequestIdHeader, requestGuid.ToString());
                            ctx.Response.ChunkedTransfer = resp.ChunkedTransferEncoding;

                            #endregion

                            #region Send-Response

                            if (!resp.ServerSentEvents)
                            {
                                if (!ctx.Response.ChunkedTransfer)
                                {
                                    await ctx.Response.Send(resp.DataAsBytes);
                                }
                                else
                                {
                                    if (resp.DataAsBytes.Length > 0)
                                    {
                                        for (int i = 0; i < resp.DataAsBytes.Length; i += BUFFER_SIZE)
                                        {
                                            int currentChunkSize = Math.Min(BUFFER_SIZE, resp.DataAsBytes.Length - i);

                                            byte[] chunk = new byte[currentChunkSize];
                                            Array.Copy(resp.DataAsBytes, i, chunk, 0, currentChunkSize);

                                            if (chunk.Length == BUFFER_SIZE) await ctx.Response.SendChunk(chunk, false).ConfigureAwait(false);
                                            else await ctx.Response.SendChunk(chunk, true).ConfigureAwait(false);
                                        }
                                    }
                                    else
                                    {
                                        await ctx.Response.SendChunk(Array.Empty<byte>(), true).ConfigureAwait(false);
                                    }
                                }
                            }
                            else
                            {
                                ctx.Response.ProtocolVersion = "HTTP/1.1";
                                ctx.Response.ServerSentEvents = true;

                                while (true)
                                {
                                    ServerSentEvent sse = await resp.ReadEventAsync();

                                    if (sse == null)
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        if (!String.IsNullOrEmpty(sse.Data))
                                        {
                                            await ctx.Response.SendEvent(sse.Data, false);
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }

                                await ctx.Response.SendEvent(null, true);
                            }

                            #endregion

                            return true;
                        }
                        else
                        {
                            _Logging.Warn(_Header + "no response from origin " + url);
                            return false;
                        }

                        #endregion
                    }
                }
                catch (System.Net.Http.HttpRequestException hre)
                {
                    _Logging.Warn(
                        _Header
                        + "exception proxying request to origin " + origin.Identifier
                        + " for endpoint " + endpoint.Endpoint.Identifier
                        + " for request " + requestGuid.ToString()
                        + ": " + hre.Message);

                    return false;
                }
                catch (SocketException se)
                {
                    _Logging.Warn(
                        _Header
                        + "exception proxying request to origin " + origin.Identifier
                        + " for endpoint " + endpoint.Endpoint.Identifier
                        + " for request " + requestGuid.ToString()
                        + ": " + se.Message);

                    return false;
                }
                catch (Exception e)
                {
                    _Logging.Warn(
                        _Header
                        + "exception proxying request to origin " + origin.Identifier
                        + " for endpoint " + endpoint.Endpoint.Identifier
                        + " for request " + requestGuid.ToString()
                        + Environment.NewLine
                        + e.ToString());

                    return false;
                }
                finally
                {
                    ts.End = DateTime.UtcNow;
                    _Logging.Debug(
                        _Header
                        + "completed request " + requestGuid.ToString() + " "
                        + "origin " + origin.Identifier + " "
                        + "endpoint " + endpoint.Endpoint.Identifier + " "
                        + (resp != null ? resp.StatusCode : "0") + " "
                        + "(" + ts.TotalMs + "ms)");

                    if (resp != null) resp.Dispose();

                    origin.Semaphore.Release();
                    Interlocked.Decrement(ref origin.ActiveRequests);
                }

                #endregion
            }
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
