namespace SampleApplication
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// A single backend origin server hosted with WatsonWebserver. Every response identifies the node
    /// that served it, so the effect of Switchboard's load balancing and per-route origin sets is
    /// visible to the caller. Each origin answers the same set of routes; Switchboard is responsible
    /// for deciding which origins are eligible for a given route.
    /// </summary>
    public sealed class SampleOriginServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Node number this origin identifies as in its responses. Minimum is 1.
        /// </summary>
        public int NodeNumber
        {
            get { return _NodeNumber; }
        }

        /// <summary>
        /// TCP port this origin listens on (bound to localhost). Range is 1 to 65535.
        /// </summary>
        public int Port
        {
            get { return _Port; }
        }

        #endregion

        #region Private-Members

        private readonly int _NodeNumber;
        private readonly int _Port;
        private readonly Webserver _Server;
        private bool _Disposed;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize (but do not start) an origin server for the given node number and port.
        /// </summary>
        /// <param name="nodeNumber">Node number echoed in responses. Minimum is 1.</param>
        /// <param name="port">TCP port to bind on localhost. Range is 1 to 65535.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="nodeNumber"/> is less than 1, or <paramref name="port"/> is outside 1 to 65535.</exception>
        public SampleOriginServer(int nodeNumber, int port)
        {
            if (nodeNumber < 1) throw new ArgumentOutOfRangeException(nameof(nodeNumber), "Node number must be 1 or greater.");
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

            _NodeNumber = nodeNumber;
            _Port = port;

            WebserverSettings settings = new WebserverSettings();
            // Bind the IPv4 loopback explicitly. Using "localhost" makes the proxy resolve the origin
            // to IPv6 (::1) first and fall back to IPv4, adding a large per-request connect delay.
            settings.Hostname = "127.0.0.1";
            settings.Port = _Port;

            _Server = new Webserver(settings, DefaultRoute);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start listening for requests on the configured port.
        /// </summary>
        public void Start()
        {
            _Server.Start();
        }

        /// <summary>
        /// Stop listening and release the underlying webserver.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        private void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                try
                {
                    _Server.Stop();
                }
                catch (Exception)
                {
                    // Ignore shutdown races when stopping the listener.
                }

                _Server.Dispose();
            }

            _Disposed = true;
        }

        // WatsonWebserver route handler. Its signature is fixed by the framework, so it cannot accept
        // a CancellationToken.
        private async Task DefaultRoute(HttpContextBase ctx)
        {
            string path = ctx.Request.Url.RawWithoutQuery;
            HttpMethod method = ctx.Request.Method;

            ctx.Response.ContentType = "text/plain";

            if (method == HttpMethod.GET && path == "/")
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send("Hello from node " + _NodeNumber).ConfigureAwait(false);
                return;
            }

            if (method == HttpMethod.GET && path == "/route1")
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send("Hello from route1, served by node " + _NodeNumber + " (valid values: 1 or 2)").ConfigureAwait(false);
                return;
            }

            if (method == HttpMethod.GET && path == "/route2")
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send("Hello from route2, served by node " + _NodeNumber + " (valid values: 2 or 3)").ConfigureAwait(false);
                return;
            }

            if (method == HttpMethod.GET && path == "/route3")
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send("Hello from route3, served by node " + _NodeNumber + " (valid values: 1 or 3)").ConfigureAwait(false);
                return;
            }

            // URL rewrite demonstration. Switchboard rewrites the client's original request
            // "/api/users/{id}" to "/internal/v2/users/{id}" before forwarding, so this origin only
            // ever receives the rewritten path. The origin echoes the rewritten path it actually got
            // and reconstructs the original by reversing this sample's known rewrite rule, so the
            // response makes both URLs visible to the caller.
            string rewrittenPrefix = "/internal/v2/users/";
            string originalPrefix = "/api/users/";
            if (method == HttpMethod.GET && path.StartsWith(rewrittenPrefix, StringComparison.Ordinal))
            {
                string userId = path.Substring(rewrittenPrefix.Length);
                string originalUrl = originalPrefix + userId;

                ctx.Response.StatusCode = 200;
                await ctx.Response.Send(
                    "Hello from the URL rewrite demo, served by node " + _NodeNumber + "." + Environment.NewLine +
                    "  Original URL (requested by the client): " + originalUrl + Environment.NewLine +
                    "  Rewritten URL (received by this origin): " + path).ConfigureAwait(false);
                return;
            }

            if (method == HttpMethod.POST && path == "/echo")
            {
                string body = ctx.Request.DataAsString ?? String.Empty;
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send("Hello from the echo route, served by node " + _NodeNumber + ".  You said: " + body).ConfigureAwait(false);
                return;
            }

            ctx.Response.StatusCode = 404;
            await ctx.Response.Send("No such route on node " + _NodeNumber).ConfigureAwait(false);
        }

        #endregion
    }
}
