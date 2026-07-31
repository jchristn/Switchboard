namespace Switchboard.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel.Design;
    using System.Diagnostics;
    using System.Globalization;
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
    using Switchboard.Core.Telemetry;
    using SyslogLogging;
    using Timestamps;
    using UrlMatcher;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using SwitchboardApiErrorResponse = Switchboard.Core.ApiErrorResponse;
    using SwitchboardAuthenticationResultEnum = Switchboard.Core.AuthenticationResultEnum;
    using SwitchboardAuthorizationResultEnum = Switchboard.Core.AuthorizationResultEnum;

    /// <summary>
    /// Gateway service.
    /// </summary>
    public class GatewayService : IDisposable
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        /// <summary>
        /// Request history capture service.
        /// If set, requests will be captured for history tracking.
        /// </summary>
        public RequestHistoryCaptureService RequestHistoryService { get; set; } = null;

        /// <summary>
        /// Logging module.
        /// </summary>
        public LoggingModule Logging
        {
            get => _Logging;
            set => _Logging = value ?? throw new ArgumentNullException(nameof(Logging));
        }

        /// <summary>
        /// Smoothing factor (alpha) for the per-origin exponentially-weighted moving average of response
        /// latency used by the <see cref="LoadBalancingMode.LatencyBased"/> mode. Higher values weight
        /// recent samples more heavily. Default is 0.3. Minimum is 0.01. Maximum is 1.0. Values are clamped.
        /// </summary>
        public double EwmaSmoothingFactor
        {
            get => _EwmaSmoothingFactor;
            set
            {
                if (value < 0.01) value = 0.01;
                if (value > 1.0) value = 1.0;
                _EwmaSmoothingFactor = value;
            }
        }

        #endregion

        #region Private-Members

        private readonly string _Header = "[GatewayService] ";
        private SwitchboardSettings _Settings = null;
        private SwitchboardCallbacks _Callbacks = null;
        private LoggingModule _Logging = null;
        private Serializer _Serializer = null;
        private Random _Random = new Random(Guid.NewGuid().GetHashCode());
        private double _EwmaSmoothingFactor = 0.3;
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

            responseHeaders.Add("Access-Control-Allow-Methods", "OPTIONS, HEAD, GET, PUT, POST, DELETE, PATCH");
            responseHeaders.Add("Access-Control-Allow-Headers", "*, Content-Type, X-Requested-With, " + headers);
            responseHeaders.Add("Access-Control-Expose-Headers", "Content-Type, X-Requested-With, " + headers);
            responseHeaders.Add("Access-Control-Allow-Origin", "*");
            responseHeaders.Add("Access-Control-Max-Age", "86400");
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
            RequestCaptureContext captureContext = null;

            // Bounded labels for the request-level metrics recorded in the finally block. The endpoint
            // identifier stays "none" until a matching endpoint is found.
            string telemetryEndpoint = "none";
            string telemetryMethod = ctx.Request.Method.ToString();

            // Start request capture if enabled
            if (RequestHistoryService != null)
            {
                captureContext = RequestHistoryService.BeginCapture(requestGuid);
                captureContext.HttpMethod = ctx.Request.Method.ToString();
                captureContext.RequestPath = ctx.Request.Url.RawWithoutQuery;
                captureContext.QueryString = ctx.Request.Query?.Querystring;
                captureContext.ClientIp = ctx.Request.Source.IpAddress;
                captureContext.RequestBodySize = ctx.Request.ContentLength;

                // Capture request headers
                if (ctx.Request.Headers != null && ctx.Request.Headers.Count > 0)
                {
                    captureContext.RequestHeaders = new Dictionary<string, string>();
                    foreach (string key in ctx.Request.Headers.AllKeys)
                    {
                        if (!String.IsNullOrEmpty(key))
                        {
                            captureContext.RequestHeaders[key] = ctx.Request.Headers.Get(key) ?? "";
                        }
                    }
                }

            }

            try
            {
                MatchingApiEndpoint match = FindApiEndpoint(ctx);
                if (match == null)
                {
                    _Logging.Warn(_Header + "no API endpoint found for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                    SwitchboardTelemetry.RecordRejection(400);
                    ctx.Response.StatusCode = 400;
                    ctx.Response.ContentType = Constants.JsonContentType;
                    await ctx.Response.Send(_Serializer.SerializeJson(new SwitchboardApiErrorResponse(ApiErrorEnum.BadRequest, null, "No matching API endpoint found"), true));

                    if (captureContext != null)
                    {
                        captureContext.StatusCode = 400;
                        captureContext.ErrorMessage = "No matching API endpoint found";
                    }
                    return;
                }

                telemetryEndpoint = match.Endpoint.Identifier;

                if (captureContext != null)
                {
                    captureContext.EndpointIdentifier = match.Endpoint.Identifier;
                    captureContext.Endpoint = match.Endpoint;
                }

                if (match.Endpoint.LogRequestFull)
                    _Logging.Debug(_Header + "incoming request:" + Environment.NewLine + _Serializer.SerializeJson(ctx.Request, true));

                if (match.Endpoint.BlockHttp10 && ctx.Request.ProtocolVersion.Equals("HTTP/1.0"))
                {
                    _Logging.Debug(_Header + "denying HTTP/1.0 request due to API endpoint configuration");
                    SwitchboardTelemetry.RecordRejection(505);
                    ctx.Response.StatusCode = 505;
                    ctx.Response.ContentType = Constants.JsonContentType;
                    await ctx.Response.Send(_Serializer.SerializeJson(new SwitchboardApiErrorResponse(ApiErrorEnum.UnsupportedHttpVersion), true));

                    if (captureContext != null)
                    {
                        captureContext.StatusCode = 505;
                        captureContext.ErrorMessage = "HTTP/1.0 not supported";
                    }
                    return;
                }

                if (match.AuthRequired)
                {
                    if (_Callbacks == null || _Callbacks.AuthenticateAndAuthorize == null)
                    {
                        _Logging.Warn(_Header + "API endpoint " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " requires auth but no auth callback set");
                        SwitchboardTelemetry.RecordRejection(401);
                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = Constants.JsonContentType;
                        await ctx.Response.Send(_Serializer.SerializeJson(new SwitchboardApiErrorResponse(ApiErrorEnum.AuthenticationFailed), true));

                        if (captureContext != null)
                        {
                            captureContext.StatusCode = 401;
                            captureContext.ErrorMessage = "Authentication required but no callback set";
                        }
                        return;
                    }

                    authContext = await _Callbacks.AuthenticateAndAuthorize(ctx);
                    if (authContext.Authentication.Result != SwitchboardAuthenticationResultEnum.Success
                        && authContext.Authorization.Result != SwitchboardAuthorizationResultEnum.Success)
                    {
                        _Logging.Warn(
                            _Header +
                            "auth failure reported for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " " +
                            "(" + authContext.Authentication.Result + "/" + authContext.Authorization.Result + ")" +
                            ": " + authContext.FailureMessage);

                        SwitchboardTelemetry.RecordRejection(401);
                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = Constants.JsonContentType;
                        await ctx.Response.Send(_Serializer.SerializeJson(new SwitchboardApiErrorResponse(ApiErrorEnum.AuthenticationFailed, null, authContext.FailureMessage), true));

                        if (captureContext != null)
                        {
                            captureContext.StatusCode = 401;
                            captureContext.ErrorMessage = authContext.FailureMessage;
                        }
                        return;
                    }

                    if (captureContext != null)
                    {
                        captureContext.WasAuthenticated = true;
                    }
                }

                // The request-size limit is origin-independent, so enforce it before selecting an origin.
                if (match.Endpoint.MaxRequestBodySize > 0 && ctx.Request.ContentLength > match.Endpoint.MaxRequestBodySize)
                {
                    _Logging.Warn(_Header + "request too large from " + ctx.Request.Source.IpAddress + ": " + ctx.Request.ContentLength + " bytes");
                    SwitchboardTelemetry.RecordRejection(413);
                    ctx.Response.StatusCode = 413;
                    ctx.Response.ContentType = Constants.JsonContentType;
                    await ctx.Response.Send(_Serializer.SerializeJson(new SwitchboardApiErrorResponse(ApiErrorEnum.TooLarge, null, "Your request was too large"), true));

                    if (captureContext != null)
                    {
                        captureContext.StatusCode = 413;
                        captureContext.ErrorMessage = "Request too large";
                    }
                    return;
                }

                // Select an origin and proxy the request, retrying against other origins on failure when the
                // endpoint permits it and the method is idempotent. Never retry once bytes have been sent.
                HashSet<string> triedOrigins = new HashSet<string>(StringComparer.Ordinal);
                bool idempotent = IsIdempotentMethod(ctx.Request.Method);
                int maxAttempts = 1 + (idempotent ? match.Endpoint.MaxRetries : 0);
                ProxyOutcome outcome = ProxyOutcome.Failed;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    OriginServer origin = FindOriginServer(match.Endpoint, ctx, triedOrigins);
                    if (origin == null) break;
                    triedOrigins.Add(origin.Identifier);

                    SwitchboardTelemetry.RecordSelection(match.Endpoint.Identifier, origin.Identifier);
                    if (attempt > 0) SwitchboardTelemetry.RecordRetry(match.Endpoint.Identifier);

                    if (captureContext != null)
                    {
                        captureContext.OriginIdentifier = origin.Identifier;
                        captureContext.Origin = origin;
                    }

                    if (captureContext != null
                        && !ctx.Request.ChunkedTransfer
                        && (_Settings.RequestHistory.CaptureRequestBody || match.Endpoint.CaptureRequestBody || origin.CaptureRequestBody))
                    {
                        CaptureBufferedRequestBody(captureContext, ctx.Request.DataAsBytes);
                    }

                    int totalRequests =
                        Volatile.Read(ref origin.ActiveRequests) +
                        Volatile.Read(ref origin.PendingRequests);

                    if (totalRequests > origin.RateLimitRequestsThreshold)
                    {
                        _Logging.Warn(_Header + "too many active requests for origin " + origin.Identifier + ", sending 429 response to request from " + ctx.Request.Source.IpAddress);
                        SwitchboardTelemetry.RecordRejection(429);
                        ctx.Response.StatusCode = 429;
                        ctx.Response.ContentType = Constants.JsonContentType;
                        await ctx.Response.Send(_Serializer.SerializeJson(new SwitchboardApiErrorResponse(ApiErrorEnum.SlowDown)));

                        if (captureContext != null)
                        {
                            captureContext.StatusCode = 429;
                            captureContext.ErrorMessage = "Rate limit exceeded";
                        }
                        return;
                    }

                    Interlocked.Increment(ref origin.PendingRequests);

                    bool lastAttempt = (attempt == maxAttempts - 1);
                    outcome = await ProxyRequest(
                        requestGuid,
                        ctx,
                        match,
                        origin,
                        authContext,
                        captureContext,
                        lastAttempt).ConfigureAwait(false);

                    if (outcome == ProxyOutcome.Completed) break;

                    if (outcome == ProxyOutcome.FailedResponseStarted)
                    {
                        _Logging.Warn(_Header + "streaming response from " + origin.Identifier + " failed after bytes were sent; cannot retry request " + requestGuid.ToString());
                        if (captureContext != null)
                        {
                            captureContext.StatusCode = ctx.Response.StatusCode;
                            captureContext.ErrorMessage = "Origin response failed after response started";
                        }
                        return;
                    }

                    if (!lastAttempt) SwitchboardTelemetry.RecordFailover(match.Endpoint.Identifier);
                    _Logging.Warn(_Header + "attempt " + (attempt + 1) + " to origin " + origin.Identifier + " failed for endpoint " + match.Endpoint.Identifier + (lastAttempt ? "; no further origins to try" : "; retrying against another origin"));
                }

                if (outcome != ProxyOutcome.Completed)
                {
                    _Logging.Warn(_Header + "no successful response for API endpoint " + match.Endpoint.Identifier + " after " + triedOrigins.Count + " origin attempt(s)");
                    SwitchboardTelemetry.RecordRejection(502);
                    ctx.Response.StatusCode = 502;
                    ctx.Response.ContentType = Constants.JsonContentType;
                    await ctx.Response.Send(_Serializer.SerializeJson(new SwitchboardApiErrorResponse(ApiErrorEnum.BadGateway, null, "No origin servers are available to service your request"), true));

                    if (captureContext != null)
                    {
                        captureContext.StatusCode = 502;
                        captureContext.ErrorMessage = "No response from origin";
                    }
                    return;
                }

                if (captureContext != null)
                {
                    captureContext.StatusCode = ctx.Response.StatusCode;
                    captureContext.ResponseBodySize = ctx.Response.ContentLength;
                }
            }
            catch (Exception e)
            {
                _Logging.Warn(_Header + "exception:" + Environment.NewLine + e.ToString());
                SwitchboardTelemetry.RecordRejection(500);
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send();

                if (captureContext != null)
                {
                    captureContext.StatusCode = 500;
                    captureContext.ErrorMessage = e.Message;
                }
            }
            finally
            {
                // Record the request-level metrics from the final response, regardless of outcome.
                SwitchboardTelemetry.RecordRequest(telemetryEndpoint, telemetryMethod, ctx.Response.StatusCode);
                SwitchboardTelemetry.RecordBodySizes(telemetryEndpoint, ctx.Request.ContentLength, ctx.Response.ContentLength);

                // End request capture
                if (captureContext != null && RequestHistoryService != null)
                {
                    _ = RequestHistoryService.EndCaptureAsync(captureContext, ctx.Token);
                }
            }
        }

        #endregion

        #region Private-Methods

        private byte[] AppendNewLine(byte[] data)
        {
            if (data == null) return null;

            // RestWrapper.ReadLineAsync() strips line endings (\n, \r\n, or \r)
            // We need to restore a line ending so the next ReadLineAsync() call
            // can properly delimit chunks. Use \n for consistency across platforms.
            byte[] newLine = new byte[] { 0x0A }; // \n
            byte[] result = new byte[data.Length + newLine.Length];

            Buffer.BlockCopy(data, 0, result, 0, data.Length);
            Buffer.BlockCopy(newLine, 0, result, data.Length, newLine.Length);

            return result;
        }

        private void CaptureBufferedRequestBody(RequestCaptureContext captureContext, byte[] data)
        {
            if (captureContext == null) throw new ArgumentNullException(nameof(captureContext));
            if (data == null || data.Length < 1) return;

            try
            {
                captureContext.RequestBody = Encoding.UTF8.GetString(data);
            }
            catch
            {
                // Binary data that can't be converted to UTF-8
                captureContext.RequestBody = "[binary data: " + data.Length + " bytes]";
            }
        }

        private bool ShouldForwardRequestHeader(string key, ApiEndpoint endpoint)
        {
            if (String.IsNullOrEmpty(key)) return false;

            string lower = key.ToLowerInvariant();

            switch (lower)
            {
                case "connection":
                case "content-length":
                case "host":
                case "keep-alive":
                case "proxy-connection":
                case "te":
                case "trailer":
                case "transfer-encoding":
                case "upgrade":
                    return false;
            }

            if (endpoint != null)
            {
                if (endpoint.BlockedHeaders != null
                    && endpoint.BlockedHeaders.Any(h => !String.IsNullOrEmpty(h) && h.Equals(lower, StringComparison.OrdinalIgnoreCase)))
                    return false;

                if (endpoint.UseGlobalBlockedHeaders
                    && _Settings.BlockedHeaders != null
                    && _Settings.BlockedHeaders.Any(h => !String.IsNullOrEmpty(h) && h.Equals(lower, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            return true;
        }

        private bool TryExtractChunkedPayload(byte[] data, out byte[] payload)
        {
            payload = null;
            if (data == null || data.Length < 1) return false;

            byte[] current = data;
            bool extracted = false;

            while (TryFlattenChunkedBody(current, out byte[] next))
            {
                current = next;
                extracted = true;
            }

            if (!extracted) return false;

            payload = current ?? Array.Empty<byte>();
            return true;
        }

        private bool TryFlattenChunkedBody(byte[] data, out byte[] payload)
        {
            payload = null;
            if (data == null || data.Length < 1) return false;

            int offset = 0;
            List<byte> flattened = new List<byte>();

            while (offset < data.Length)
            {
                if (!TryReadAsciiLine(data, ref offset, out string lengthLine)) return false;
                if (String.IsNullOrWhiteSpace(lengthLine)) continue;

                string[] lengthParts = lengthLine.Split(';');
                string lengthText = lengthParts[0].Trim();

                if (!Int32.TryParse(lengthText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int chunkLength)
                    || chunkLength < 0)
                {
                    return false;
                }

                if (chunkLength == 0)
                {
                    payload = flattened.ToArray();
                    return true;
                }

                if (offset + chunkLength > data.Length) return false;

                for (int i = 0; i < chunkLength; i++)
                {
                    flattened.Add(data[offset + i]);
                }

                offset += chunkLength;

                if (!ConsumeLineEnding(data, ref offset)) return false;
            }

            return false;
        }

        private bool TryReadAsciiLine(byte[] data, ref int offset, out string line)
        {
            line = null;
            if (data == null || offset < 0 || offset >= data.Length) return false;

            int start = offset;

            while (offset < data.Length)
            {
                if (data[offset] == 0x0A)
                {
                    int length = offset - start;
                    if (length > 0 && data[offset - 1] == 0x0D) length--;
                    line = Encoding.ASCII.GetString(data, start, length);
                    offset++;
                    return true;
                }

                offset++;
            }

            return false;
        }

        private bool ConsumeLineEnding(byte[] data, ref int offset)
        {
            if (data == null || offset < 0 || offset >= data.Length) return false;

            if (data[offset] == 0x0D)
            {
                if (offset + 1 >= data.Length || data[offset + 1] != 0x0A) return false;
                offset += 2;
                return true;
            }

            if (data[offset] == 0x0A)
            {
                offset++;
                return true;
            }

            return false;
        }

        private async Task GetRootRoute(HttpContextBase ctx)
        {
            // A configured API endpoint for GET / takes precedence over the built-in homepage, so the
            // root path can be proxied like any other route; the homepage is only a fallback.
            if (FindApiEndpoint(ctx) != null)
            {
                await DefaultRoute(ctx).ConfigureAwait(false);
                return;
            }

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = Constants.HtmlContentType;
            await ctx.Response.Send(Constants.HtmlHomepage).ConfigureAwait(false);
        }

        private async Task HeadRootRoute(HttpContextBase ctx)
        {
            // A configured API endpoint for HEAD / takes precedence over the built-in homepage.
            if (FindApiEndpoint(ctx) != null)
            {
                await DefaultRoute(ctx).ConfigureAwait(false);
                return;
            }

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = Constants.TextContentType;
            await ctx.Response.Send().ConfigureAwait(false);
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

        private OriginServer FindOriginServer(ApiEndpoint endpoint, HttpContextBase ctx, ISet<string> exclude)
        {
            if (endpoint == null) return null;

            string clientIp = ctx?.Request?.Source?.IpAddress;
            System.Collections.Specialized.NameValueCollection headers = ctx?.Request?.Headers;

            lock (endpoint.Lock)
            {
                return OriginSelector.Select(endpoint, _Settings.Origins, clientIp, headers, exclude, _Random, DateTime.UtcNow);
            }
        }

        // Idempotent methods may be safely retried against another origin after a failed attempt.
        private static bool IsIdempotentMethod(WatsonWebserver.Core.HttpMethod method)
        {
            switch (method)
            {
                case WatsonWebserver.Core.HttpMethod.GET:
                case WatsonWebserver.Core.HttpMethod.HEAD:
                case WatsonWebserver.Core.HttpMethod.OPTIONS:
                case WatsonWebserver.Core.HttpMethod.PUT:
                case WatsonWebserver.Core.HttpMethod.DELETE:
                case WatsonWebserver.Core.HttpMethod.TRACE:
                    return true;
                default:
                    return false;
            }
        }

        // Record a proxied-request outcome onto the origin for passive health checking and latency-aware
        // load balancing. A null response or a 5xx status is a failure; enough consecutive failures ejects
        // the origin from routing for its ejection window. Successful latencies feed the EWMA.
        private void RecordProxyOutcome(OriginServer origin, string endpointId, int statusCode, bool hadResponse, double latencyMs)
        {
            bool failure = !hadResponse || statusCode >= 500;
            bool ejectedNow = false;
            DateTime now = DateTime.UtcNow;

            lock (origin.Lock)
            {
                if (!failure)
                {
                    origin.ConsecutiveProxyFailures = 0;
                    origin.EjectedUntilUtc = null;

                    if (!origin.HasLatencySample)
                    {
                        origin.EwmaLatencyMs = latencyMs;
                        origin.HasLatencySample = true;
                    }
                    else
                    {
                        origin.EwmaLatencyMs = (_EwmaSmoothingFactor * latencyMs) + ((1.0 - _EwmaSmoothingFactor) * origin.EwmaLatencyMs);
                    }
                }
                else
                {
                    origin.ConsecutiveProxyFailures++;
                    if (origin.MaxFailures > 0 && origin.ConsecutiveProxyFailures >= origin.MaxFailures)
                    {
                        origin.EjectedUntilUtc = now.AddMilliseconds(origin.EjectionDurationMs);
                        origin.ConsecutiveProxyFailures = 0;
                        ejectedNow = true;
                        _Logging.Warn(_Header + "origin " + origin.Identifier + " ejected from routing for " + origin.EjectionDurationMs + "ms after " + origin.MaxFailures + " consecutive failures");
                    }
                }
            }

            // Emit telemetry outside the origin lock. When there was no response (null), the origin request
            // still counts, tagged with code 0.
            SwitchboardTelemetry.RecordOriginRequest(origin.Identifier, statusCode);
            SwitchboardTelemetry.RecordDuration(endpointId, origin.Identifier, latencyMs / 1000.0);
            if (ejectedNow) SwitchboardTelemetry.RecordEjection(origin.Identifier);
        }

        // Start the per-proxy span, continuing the client's trace when an inbound W3C traceparent is present.
        // Returns null when no tracing provider is listening, in which case the caller records nothing.
        private static Activity StartProxyActivity(HttpContextBase ctx, string endpointId, string originId, string method)
        {
            ActivityContext parentContext = default;
            string traceparent = ctx?.Request?.Headers?.Get("traceparent");
            if (!String.IsNullOrEmpty(traceparent))
            {
                string tracestate = ctx.Request.Headers.Get("tracestate");
                if (ActivityContext.TryParse(traceparent, tracestate, out ActivityContext parsed))
                    parentContext = parsed;
            }

            Activity activity = SwitchboardTelemetry.ActivitySource.StartActivity(
                "proxy " + endpointId,
                ActivityKind.Server,
                parentContext);

            if (activity != null)
            {
                activity.SetTag("switchboard.endpoint", endpointId);
                activity.SetTag("switchboard.origin", originId);
                activity.SetTag("http.request.method", method);
                activity.SetTag("http.route", endpointId);
            }

            return activity;
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

        private async Task<ProxyOutcome> ProxyRequest(
            Guid requestGuid,
            HttpContextBase ctx,
            MatchingApiEndpoint endpoint,
            OriginServer origin,
            AuthContext authResult,
            RequestCaptureContext captureContext = null,
            bool isLastAttempt = true)
        {
            _Logging.Debug(_Header + "proxying request to " + origin.Identifier + " for endpoint " + endpoint.Endpoint.Identifier + " for request " + requestGuid.ToString());

            RestResponse resp = null;
            bool responseStarted = false;

            using (Activity activity = StartProxyActivity(ctx, endpoint.Endpoint.Identifier, origin.Identifier, ctx.Request.Method.ToString()))
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

                        // Propagate the active trace context downstream so the origin can continue the trace.
                        if (_Settings.Telemetry.Traces.PropagateToOrigin)
                        {
                            Activity current = Activity.Current;
                            if (current != null && current.Id != null)
                            {
                                req.Headers.Add("traceparent", current.Id);
                                if (!String.IsNullOrEmpty(current.TraceStateString))
                                    req.Headers.Add("tracestate", current.TraceStateString);
                            }
                        }

                        if (authResult != null && endpoint.Endpoint.IncludeAuthContextHeader)
                            req.Headers.Add(Constants.AuthContextHeader, authResult.ToBase64String());

                        if (ctx.Request.Headers != null && ctx.Request.Headers.Count > 0)
                        {
                            foreach (string key in ctx.Request.Headers.Keys)
                            {
                                if (!ShouldForwardRequestHeader(key, endpoint.Endpoint))
                                    continue;

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
                            if (ctx.Request.ChunkedTransfer)
                            {
                                _Logging.Debug(_Header + "request body logging skipped for chunked transfer request");
                            }
                            else if (ctx.Request.DataAsBytes != null)
                            {
                                _Logging.Debug(
                                    _Header
                                    + "request body (" + ctx.Request.DataAsBytes.Length + " bytes): "
                                    + Environment.NewLine
                                    + Encoding.UTF8.GetString(ctx.Request.DataAsBytes));
                            }

                            _Logging.Debug(_Header + "using content-type: " + req.ContentType);
                        }

                        #endregion

                        #region Send-Request

                        _Logging.Debug(_Header + "request transfer encoding: " + ctx.Request.Headers.Get("Transfer-Encoding") + ", chunked: " + ctx.Request.ChunkedTransfer);

                        if (ctx.Request.ChunkedTransfer)
                        {
                            #region Chunked-Transfer-Request

                            if (!String.IsNullOrEmpty(ctx.Request.ContentType))
                                req.ContentType = ctx.Request.ContentType;
                            else
                                req.ContentType = Constants.BinaryContentType;

                            req.ChunkedTransfer = true;

                            _Logging.Debug(_Header + "forwarding chunked request to " + origin.Identifier);

                            if (TryExtractChunkedPayload(ctx.Request.DataAsBytes, out byte[] normalizedPayload))
                            {
                                _Logging.Debug(_Header + "forwarding normalized buffered chunked payload: " + normalizedPayload.Length + " bytes");
                                resp = await req.SendChunkAsync(normalizedPayload, true);
                            }
                            else if (ctx.Request.DataAsBytes != null && ctx.Request.DataAsBytes.Length > 0)
                            {
                                _Logging.Debug(_Header + "forwarding buffered chunked request as a single chunk: " + ctx.Request.DataAsBytes.Length + " bytes");
                                resp = await req.SendChunkAsync(ctx.Request.DataAsBytes, true);
                            }
                            else
                            {
                                bool finalChunk = false;
                                while (!finalChunk)
                                {
                                    Chunk chunk = await ctx.Request.ReadChunk();
                                    finalChunk = (chunk != null && chunk.IsFinal);

                                    _Logging.Debug(_Header + "forwarding streamed chunk: " + chunk?.Length + " bytes, final: " + finalChunk);

                                    if (chunk != null && chunk.Length > 0)
                                    {
                                        resp = await req.SendChunkAsync(chunk.Data, finalChunk);
                                    }
                                    else if (finalChunk)
                                    {
                                        resp = await req.SendChunkAsync(Array.Empty<byte>(), true);
                                    }
                                }
                            }

                            #endregion
                        }
                        else if (ctx.Request.DataAsBytes != null && ctx.Request.DataAsBytes.Length > 0)
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

                        #endregion

                        #region Process-Response

                        if (resp != null)
                        {
                            #region Log-Response-Body

                            if (endpoint.Endpoint.LogResponseBody || origin.LogResponseBody)
                            {
                                if (resp.ChunkedTransferEncoding)
                                {
                                    _Logging.Debug(_Header + "chunked transfer response body received");
                                }
                                else if (resp.ServerSentEvents)
                                {
                                    _Logging.Debug(_Header + "server-sent events response body received");
                                }
                                else
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
                            }

                            #endregion

                            // When the response is a retryable failure (5xx) and another attempt remains,
                            // do not forward it to the client; return Failed so the caller retries another
                            // origin. Streaming responses cannot be deferred once received.
                            bool isStreamingResponse = resp.ServerSentEvents || resp.ChunkedTransferEncoding;
                            if (!isLastAttempt
                                && !isStreamingResponse
                                && endpoint.Endpoint.RetryOn5xx
                                && resp.StatusCode >= 500)
                            {
                                _Logging.Debug(_Header + "deferring HTTP " + resp.StatusCode + " from origin " + origin.Identifier + " for retry of request " + requestGuid.ToString());
                                return ProxyOutcome.Failed;
                            }

                            #region Capture-Response-Data

                            if (captureContext != null)
                            {
                                // Capture response headers
                                if (resp.Headers != null && resp.Headers.Count > 0)
                                {
                                    captureContext.ResponseHeaders = new Dictionary<string, string>();
                                    foreach (string key in resp.Headers.AllKeys)
                                    {
                                        if (!String.IsNullOrEmpty(key))
                                        {
                                            captureContext.ResponseHeaders[key] = resp.Headers.Get(key) ?? "";
                                        }
                                    }
                                }

                                // Capture response body (only for non-streaming responses)
                                if (!resp.ServerSentEvents && !resp.ChunkedTransferEncoding)
                                {
                                    captureContext.ResponseBodySize = resp.DataAsBytes?.Length ?? 0;
                                    if (resp.DataAsBytes != null && resp.DataAsBytes.Length > 0)
                                    {
                                        try
                                        {
                                            captureContext.ResponseBody = Encoding.UTF8.GetString(resp.DataAsBytes);
                                        }
                                        catch
                                        {
                                            // Binary data that can't be converted to UTF-8
                                            captureContext.ResponseBody = "[binary data: " + resp.DataAsBytes.Length + " bytes]";
                                        }
                                    }
                                }
                                else
                                {
                                    // For streaming responses, we can't capture the full body
                                    captureContext.ResponseBody = resp.ServerSentEvents
                                        ? "[server-sent events stream]"
                                        : "[chunked transfer stream]";
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

                            responseStarted = true;

                            if (resp.ServerSentEvents)
                            {
                                ctx.Response.ProtocolVersion = "HTTP/1.1";
                                ctx.Response.ServerSentEvents = true;

                                WatsonWebserver.Core.ServerSentEvent nextEvent = null;

                                while (true)
                                {
                                    RestWrapper.ServerSentEvent sse = await resp.ReadEventAsync();

                                    if (sse == null) break;

                                    if (nextEvent != null)
                                    {
                                        await ctx.Response.SendEvent(nextEvent, false).ConfigureAwait(false);
                                    }

                                    // RestWrapper.ReadEventAsync() may leave trailing \r in the data
                                    // Strip it before forwarding since Watson.SendEvent will add proper line endings
                                    string eventData = sse.Data;
                                    if (eventData != null)
                                    {
                                        eventData = eventData.TrimEnd('\r', '\n');
                                    }

                                    nextEvent = new WatsonWebserver.Core.ServerSentEvent
                                    {
                                        Id = sse.Id,
                                        Data = eventData,
                                        Event = sse.Event,
                                        Retry = (sse.Retry != null ? sse.Retry.ToString() : null)
                                    };
                                }

                                if (nextEvent != null)
                                {
                                    await ctx.Response.SendEvent(nextEvent, true).ConfigureAwait(false);
                                }
                            }
                            else if (resp.ChunkedTransferEncoding)
                            {
                                ChunkData nextChunk = null;

                                while (true)
                                {
                                    ChunkData chunk = await resp.ReadChunkAsync().ConfigureAwait(false);
                                    if (chunk == null) break;

                                    if (nextChunk != null)
                                    {
                                        byte[] dataWithNewLine = AppendNewLine(nextChunk.Data);
                                        await ctx.Response.SendChunk(dataWithNewLine, false).ConfigureAwait(false);
                                    }

                                    nextChunk = chunk;
                                }

                                if (nextChunk != null)
                                {
                                    byte[] dataWithNewLine = AppendNewLine(nextChunk.Data);
                                    await ctx.Response.SendChunk(dataWithNewLine, true).ConfigureAwait(false);
                                }
                                else
                                {
                                    await ctx.Response.SendChunk(Array.Empty<byte>(), true).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                await ctx.Response.Send(resp.DataAsBytes);
                            }

                            #endregion

                            return ProxyOutcome.Completed;
                        }
                        else
                        {
                            _Logging.Warn(_Header + "no response from origin " + url);
                            return ProxyOutcome.Failed;
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

                    return responseStarted ? ProxyOutcome.FailedResponseStarted : ProxyOutcome.Failed;
                }
                catch (SocketException se)
                {
                    _Logging.Warn(
                        _Header
                        + "exception proxying request to origin " + origin.Identifier
                        + " for endpoint " + endpoint.Endpoint.Identifier
                        + " for request " + requestGuid.ToString()
                        + ": " + se.Message);

                    return responseStarted ? ProxyOutcome.FailedResponseStarted : ProxyOutcome.Failed;
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

                    return responseStarted ? ProxyOutcome.FailedResponseStarted : ProxyOutcome.Failed;
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

                    // Annotate the span with the outcome.
                    if (activity != null)
                    {
                        int spanCode = resp != null ? resp.StatusCode : 0;
                        activity.SetTag("http.response.status_code", spanCode);
                        if (resp == null || spanCode >= 500) activity.SetStatus(ActivityStatusCode.Error);
                    }

                    // Feed the outcome into passive health checking and latency-aware balancing before the
                    // response is disposed. A null response or a 5xx status counts as a failure.
                    RecordProxyOutcome(origin, endpoint.Endpoint.Identifier, resp != null ? resp.StatusCode : 0, resp != null, (double)ts.TotalMs);

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
