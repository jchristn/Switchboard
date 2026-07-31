namespace Test.Shared.Harness
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Threading.Tasks;

    using SerializationHelper;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Hosts a single backend origin webserver that mimics the behavior the Switchboard
    /// integration tests depend on: a health endpoint, Server-Sent Events, chunked upload
    /// echo, and REST echo of method, path, query, body, and selected headers.
    /// </summary>
    public sealed class OriginHost : IDisposable
    {
        private readonly string _ServerName;
        private readonly int _Port;
        private readonly Serializer _Serializer = new Serializer();
        private WebserverSettings _Settings;
        private Webserver? _Server;
        private bool _Disposed;
        private volatile int _ForcedStatusCode;
        private volatile int _ArtificialDelayMs;
        private volatile IReadOnlyDictionary<string, string>? _LastRequestHeaders;

        /// <summary>
        /// Initialize a new origin host.
        /// </summary>
        /// <param name="serverName">Human-readable server name echoed in responses (e.g. "Server 1").</param>
        /// <param name="port">TCP port to bind on localhost.</param>
        public OriginHost(string serverName, int port)
        {
            _ServerName = serverName ?? throw new ArgumentNullException(nameof(serverName));
            _Port = port;
            _Settings = new WebserverSettings();
            // Bind the IPv4 loopback explicitly so the proxy (and health checks) reach the origin
            // without the ~2s IPv6 (::1) connect fallback that "localhost" incurs on Windows.
            _Settings.Hostname = "127.0.0.1";
            _Settings.Port = _Port;
        }

        /// <summary>
        /// Server name echoed in responses.
        /// </summary>
        public string ServerName
        {
            get { return _ServerName; }
        }

        /// <summary>
        /// HTTP status code to force on non-health (REST echo) responses. 0 (the default) returns the
        /// normal status. Used to simulate an origin that passes active health checks but fails real
        /// traffic, for retry and passive-ejection tests. Thread-safe.
        /// </summary>
        public int ForcedStatusCode
        {
            get { return _ForcedStatusCode; }
            set { _ForcedStatusCode = value; }
        }

        /// <summary>
        /// Artificial delay, in milliseconds, added to non-health (REST echo) responses to simulate a slow
        /// origin, for latency-aware and least-connections tests. Default is 0. Thread-safe.
        /// </summary>
        public int ArtificialDelayMs
        {
            get { return _ArtificialDelayMs; }
            set { _ArtificialDelayMs = value; }
        }

        /// <summary>
        /// Case-insensitive snapshot of the headers received on the most recent non-health request, or
        /// null if no such request has been received. Used to assert propagated headers (e.g. traceparent).
        /// Thread-safe.
        /// </summary>
        public IReadOnlyDictionary<string, string>? LastRequestHeaders
        {
            get { return _LastRequestHeaders; }
        }

        /// <summary>
        /// Return the value of the named header from the most recent non-health request, or null if the
        /// header (or the request) is absent. Case-insensitive.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <returns>The header value, or null.</returns>
        public string? LastRequestHeader(string name)
        {
            IReadOnlyDictionary<string, string>? headers = _LastRequestHeaders;
            if (headers == null || name == null) return null;
            return headers.TryGetValue(name, out string? value) ? value : null;
        }

        /// <summary>
        /// Start listening.
        /// </summary>
        public void Start()
        {
            if (_Server == null)
            {
                _Server = new Webserver(_Settings, Route);
            }

            _Server.Start();
        }

        /// <summary>
        /// Stop listening (can be restarted).
        /// </summary>
        public void Stop()
        {
            _Server?.Stop();
        }

        private async Task Route(HttpContextBase ctx)
        {
            if (IsHealthCheck(ctx))
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send().ConfigureAwait(false);
                return;
            }

            CaptureRequestHeaders(ctx);
            await HandleAdvancedRequest(ctx).ConfigureAwait(false);
        }

        private void CaptureRequestHeaders(HttpContextBase ctx)
        {
            Dictionary<string, string> snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (ctx.Request.Headers != null)
            {
                foreach (string? key in ctx.Request.Headers.AllKeys)
                {
                    if (!String.IsNullOrEmpty(key))
                        snapshot[key] = ctx.Request.Headers.Get(key) ?? String.Empty;
                }
            }
            _LastRequestHeaders = snapshot;
        }

        private static bool IsHealthCheck(HttpContextBase ctx)
        {
            return ctx.Request.Method == HttpMethod.GET
                && ctx.Request.Url.RawWithoutQuery == "/";
        }

        /// <summary>
        /// Fixed chunk payload streamed by the chunked-download route (order significant).
        /// </summary>
        public static readonly string[] DownloadChunks =
        {
            "Chunk 1: This is the first chunk of data.\n",
            "Chunk 2: Here comes the second chunk.\n",
            "Chunk 3: Third chunk with more data.\n",
            "Chunk 4: Final chunk to complete the transfer.\n"
        };

        private async Task HandleAdvancedRequest(HttpContextBase ctx)
        {
            if (ctx.Request.Url.RawWithoutQuery.EndsWith("/events", StringComparison.Ordinal))
            {
                await HandleServerSentEvents(ctx).ConfigureAwait(false);
                return;
            }

            if (ctx.Request.Url.RawWithoutQuery.EndsWith("/chunked-download", StringComparison.Ordinal))
            {
                await HandleChunkedDownload(ctx).ConfigureAwait(false);
                return;
            }

            if (ctx.Request.Url.RawWithoutQuery.EndsWith("/upload", StringComparison.Ordinal))
            {
                await HandleChunkedUpload(ctx).ConfigureAwait(false);
                return;
            }

            await HandleRestRequest(ctx).ConfigureAwait(false);
        }

        private async Task HandleChunkedDownload(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            ctx.Response.ChunkedTransfer = true;

            for (int i = 0; i < DownloadChunks.Length; i++)
            {
                bool isFinal = (i == DownloadChunks.Length - 1);
                byte[] chunkBytes = Encoding.UTF8.GetBytes(DownloadChunks[i]);
                await ctx.Response.SendChunk(chunkBytes, isFinal).ConfigureAwait(false);
                if (!isFinal) await Task.Delay(50).ConfigureAwait(false);
            }
        }

        private async Task HandleServerSentEvents(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.Add("Cache-Control", "no-cache");
            ctx.Response.Headers.Add("Connection", "keep-alive");
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            ctx.Response.ServerSentEvents = true;

            await ctx.Response.SendEvent(new ServerSentEvent
            {
                Data = "Connected to " + _ServerName
            }, false).ConfigureAwait(false);

            for (int i = 1; i <= 3; i++)
            {
                await Task.Delay(100).ConfigureAwait(false);
                await ctx.Response.SendEvent(new ServerSentEvent
                {
                    Data = "Event " + i + " from " + _ServerName + " at " + DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                }, false).ConfigureAwait(false);
            }

            await ctx.Response.SendEvent(new ServerSentEvent
            {
                Data = "Connection closing from " + _ServerName
            }, true).ConfigureAwait(false);
        }

        private async Task HandleChunkedUpload(HttpContextBase ctx)
        {
            byte[] requestData = Array.Empty<byte>();
            string transferEncoding = ctx.Request.Headers.Get("Transfer-Encoding") ?? "none";

            if (ctx.Request.ChunkedTransfer)
            {
                if (TryExtractChunkedPayload(ctx.Request.DataAsBytes, out byte[]? normalizedPayload) && normalizedPayload != null)
                {
                    requestData = normalizedPayload;
                }
                else if (ctx.Request.Data != null && ctx.Request.DataAsBytes != null)
                {
                    requestData = ctx.Request.DataAsBytes;
                }
                else
                {
                    List<byte> allData = new List<byte>();
                    bool finalChunk = false;

                    while (!finalChunk)
                    {
                        Chunk chunk = await ctx.Request.ReadChunk().ConfigureAwait(false);
                        finalChunk = (chunk != null && chunk.IsFinal);

                        if (chunk != null && chunk.Length > 0)
                        {
                            allData.AddRange(chunk.Data);
                        }
                    }

                    requestData = allData.ToArray();
                }
            }
            else if (ctx.Request.Data != null && ctx.Request.DataAsBytes != null)
            {
                requestData = ctx.Request.DataAsBytes;
            }

            if (requestData.Length > 0 || transferEncoding.Contains("chunked"))
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";

                Dictionary<string, object> response = new Dictionary<string, object>
                {
                    { "server", _ServerName },
                    { "status", "completed" },
                    { "received", requestData.Length },
                    { "timestamp", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) },
                    { "transferEncoding", transferEncoding }
                };

                string responseJson = _Serializer.SerializeJson(response, true);
                await ctx.Response.Send(responseJson).ConfigureAwait(false);
            }
            else
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send("{\"error\":\"No data received\",\"server\":\"" + _ServerName + "\"}").ConfigureAwait(false);
            }
        }

        private async Task HandleRestRequest(HttpContextBase ctx)
        {
            int delay = _ArtificialDelayMs;
            if (delay > 0) await Task.Delay(delay).ConfigureAwait(false);

            string responseContent = CreateRestResponse(ctx);
            string contentType = ctx.Request.Url.RawWithoutQuery.Contains("/api/") ? "application/json" : "text/plain";

            int forced = _ForcedStatusCode;
            ctx.Response.StatusCode = forced > 0 ? forced : GetStatusCodeForMethod(ctx.Request.Method);
            ctx.Response.ContentType = contentType;
            ctx.Response.Headers.Add("X-Origin-Server", _ServerName);

            await ctx.Response.Send(responseContent).ConfigureAwait(false);
        }

        private string CreateRestResponse(HttpContextBase ctx)
        {
            string path = ctx.Request.Url.RawWithoutQuery;
            string method = ctx.Request.Method.ToString();

            if (path.Contains("/api/"))
            {
                Dictionary<string, object> response = new Dictionary<string, object>
                {
                    { "server", _ServerName },
                    { "method", method },
                    { "path", path },
                    { "timestamp", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) },
                    { "query", ctx.Request.Query.Querystring ?? "" }
                };

                if (!String.IsNullOrEmpty(ctx.Request.DataAsString))
                {
                    response["receivedData"] = ctx.Request.DataAsString;
                }

                Dictionary<string, string> receivedHeaders = new Dictionary<string, string>();
                if (ctx.Request.Headers != null)
                {
                    foreach (string? key in ctx.Request.Headers.AllKeys)
                    {
                        if (key != null && (key.StartsWith("X-", StringComparison.Ordinal) || key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)))
                        {
                            receivedHeaders[key] = ctx.Request.Headers.Get(key) ?? String.Empty;
                        }
                    }
                }

                if (receivedHeaders.Count > 0)
                {
                    response["receivedHeaders"] = receivedHeaders;
                }

                return _Serializer.SerializeJson(response, true);
            }

            return "Hello from " + _ServerName + ": " + method + " " + ctx.Request.Url.RawWithQuery;
        }

        private static int GetStatusCodeForMethod(HttpMethod method)
        {
            switch (method)
            {
                case HttpMethod.POST:
                    return 201;
                case HttpMethod.PUT:
                    return 200;
                case HttpMethod.DELETE:
                    return 204;
                case HttpMethod.PATCH:
                    return 200;
                default:
                    return 200;
            }
        }

        private static bool TryExtractChunkedPayload(byte[]? data, out byte[]? payload)
        {
            payload = null;
            if (data == null || data.Length < 1) return false;

            byte[] current = data;
            bool extracted = false;

            while (TryFlattenChunkedBody(current, out byte[]? next) && next != null)
            {
                current = next;
                extracted = true;
            }

            if (!extracted) return false;

            payload = current ?? Array.Empty<byte>();
            return true;
        }

        private static bool TryFlattenChunkedBody(byte[]? data, out byte[]? payload)
        {
            payload = null;
            if (data == null || data.Length < 1) return false;

            int offset = 0;
            List<byte> flattened = new List<byte>();

            while (offset < data.Length)
            {
                if (!TryReadAsciiLine(data, ref offset, out string? lengthLine) || lengthLine == null) return false;
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

        private static bool TryReadAsciiLine(byte[] data, ref int offset, out string? line)
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

        private static bool ConsumeLineEnding(byte[] data, ref int offset)
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

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            try
            {
                _Server?.Stop();
            }
            catch (Exception)
            {
                // ignore shutdown races
            }

            _Server?.Dispose();
            _Server = null;
        }
    }
}
