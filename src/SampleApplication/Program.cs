namespace SampleApplication
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core;
    using Switchboard.Core.Database;

    /// <summary>
    /// Sample application that runs the Switchboard daemon in-process (no Docker) as a reverse proxy
    /// and API gateway in front of three WatsonWebserver origin servers. Several routes are load
    /// balanced across different subsets of the origins so the routing and load-balancing behavior is
    /// observable from a simple HTTP client such as curl.
    /// </summary>
    public static class Program
    {
        #region Public-Members

        /// <summary>
        /// Default TCP port the Switchboard proxy listens on when no override is supplied.
        /// </summary>
        public static int DefaultProxyPort
        {
            get { return _DefaultProxyPort; }
        }

        #endregion

        #region Private-Members

        private static readonly int _DefaultProxyPort = 8000;
        private static readonly int _Node1Port = 9001;
        private static readonly int _Node2Port = 9002;
        private static readonly int _Node3Port = 9003;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Entry point. Starts the three origins and the Switchboard daemon, waits for the origins to
        /// become healthy, then runs until Ctrl+C.
        /// </summary>
        /// <param name="args">Optional command-line arguments. The first argument, when present and a valid port, overrides the proxy port (default is <see cref="DefaultProxyPort"/>).</param>
        /// <returns>A task that completes when the application shuts down.</returns>
        public static async Task Main(string[] args)
        {
            int proxyPort = ResolveProxyPort(args);

            List<SampleOriginServer> origins = new List<SampleOriginServer>
            {
                new SampleOriginServer(1, _Node1Port),
                new SampleOriginServer(2, _Node2Port),
                new SampleOriginServer(3, _Node3Port)
            };

            SwitchboardDaemon? daemon = null;

            try
            {
                foreach (SampleOriginServer origin in origins) origin.Start();

                SwitchboardSettings settings = BuildSettings(proxyPort);
                daemon = new SwitchboardDaemon(settings);

                await WaitForOriginsHealthyAsync(settings, TimeSpan.FromSeconds(15), CancellationToken.None).ConfigureAwait(false);

                PrintBanner(proxyPort);

                using (ManualResetEventSlim shutdown = new ManualResetEventSlim(false))
                {
                    Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
                    {
                        e.Cancel = true;
                        shutdown.Set();
                    };

                    shutdown.Wait();
                }

                Console.WriteLine();
                Console.WriteLine("Shutting down...");
            }
            finally
            {
                daemon?.Dispose();
                foreach (SampleOriginServer origin in origins) origin.Dispose();
            }
        }

        #endregion

        #region Private-Methods

        private static int ResolveProxyPort(string[] args)
        {
            if (args != null && args.Length > 0
                && Int32.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                && parsed >= 1 && parsed <= 65535)
            {
                return parsed;
            }

            return _DefaultProxyPort;
        }

        private static SwitchboardSettings BuildSettings(int proxyPort)
        {
            SwitchboardSettings settings = new SwitchboardSettings();

            settings.Webserver.Hostname = "localhost";
            settings.Webserver.Port = proxyPort;

            settings.Logging.ConsoleLogging = true;
            settings.Logging.EnableColors = true;
            settings.Logging.MinimumSeverity = 1;

            settings.Database.Type = DatabaseTypeEnum.Sqlite;
            settings.Database.Filename = "./sampleapplication.db";

            settings.Management.Enable = false;
            settings.RequestHistory.Enable = false;

            settings.Origins.Add(BuildOrigin("node1", "Node 1", _Node1Port));
            settings.Origins.Add(BuildOrigin("node2", "Node 2", _Node2Port));
            settings.Origins.Add(BuildOrigin("node3", "Node 3", _Node3Port));

            // GET / and POST /echo may be served by any node.
            Dictionary<string, List<string>> anyRoutes = new Dictionary<string, List<string>>
            {
                { "GET", new List<string> { "/" } },
                { "POST", new List<string> { "/echo" } }
            };
            settings.Endpoints.Add(BuildEndpoint("root-and-echo", "Root and echo (any node)", anyRoutes,
                new List<string> { "node1", "node2", "node3" }));

            // GET /route1 -> nodes 1 and 2.
            settings.Endpoints.Add(BuildEndpoint("route1", "Route 1 (nodes 1 and 2)",
                SingleGetRoute("/route1"), new List<string> { "node1", "node2" }));

            // GET /route2 -> nodes 2 and 3.
            settings.Endpoints.Add(BuildEndpoint("route2", "Route 2 (nodes 2 and 3)",
                SingleGetRoute("/route2"), new List<string> { "node2", "node3" }));

            // GET /route3 -> nodes 1 and 3.
            settings.Endpoints.Add(BuildEndpoint("route3", "Route 3 (nodes 1 and 3)",
                SingleGetRoute("/route3"), new List<string> { "node1", "node3" }));

            return settings;
        }

        private static Dictionary<string, List<string>> SingleGetRoute(string url)
        {
            return new Dictionary<string, List<string>>
            {
                { "GET", new List<string> { url } }
            };
        }

        private static OriginServer BuildOrigin(string identifier, string name, int port)
        {
            OriginServer origin = new OriginServer();
            origin.Identifier = identifier;
            origin.Name = name;
            // Use the IPv4 loopback rather than "localhost". "localhost" resolves to IPv6 (::1) first,
            // and because the origins listen on IPv4 only, every proxied request would otherwise wait
            // out a failed IPv6 connect before falling back to IPv4 (~2s per request).
            origin.Hostname = "127.0.0.1";
            origin.Port = port;
            origin.Ssl = false;
            origin.HealthCheckUrl = "/";
            origin.HealthCheckIntervalMs = 2000;
            origin.HealthyThreshold = 1;
            origin.UnhealthyThreshold = 2;
            origin.MaxParallelRequests = 100;
            origin.RateLimitRequestsThreshold = 1000;
            return origin;
        }

        private static ApiEndpoint BuildEndpoint(
            string identifier,
            string name,
            Dictionary<string, List<string>> parameterizedUrls,
            List<string> originIdentifiers)
        {
            ApiEndpoint endpoint = new ApiEndpoint();
            endpoint.Identifier = identifier;
            endpoint.Name = name;
            endpoint.LoadBalancing = LoadBalancingMode.RoundRobin;
            endpoint.Unauthenticated = new ApiEndpointGroup
            {
                ParameterizedUrls = parameterizedUrls
            };
            endpoint.OriginServers = originIdentifiers;
            return endpoint;
        }

        private static async Task WaitForOriginsHealthyAsync(SwitchboardSettings settings, TimeSpan timeout, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);

            while (settings.Origins.Any(o => !o.Healthy))
            {
                if (token.IsCancellationRequested) return;
                if (DateTime.UtcNow > deadline) return;
                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }

        private static void PrintBanner(int proxyPort)
        {
            string baseUrl = "http://localhost:" + proxyPort.ToString(CultureInfo.InvariantCulture);

            Console.WriteLine();
            Console.WriteLine("================================================================================");
            Console.WriteLine("  Switchboard Sample Application");
            Console.WriteLine("================================================================================");
            Console.WriteLine();
            Console.WriteLine("  Proxy listening at " + baseUrl);
            Console.WriteLine("  Origins: node 1 (:" + _Node1Port + "), node 2 (:" + _Node2Port + "), node 3 (:" + _Node3Port + ")");
            Console.WriteLine();
            Console.WriteLine("  Try these routes:");
            Console.WriteLine("    curl " + baseUrl + "/            # any node");
            Console.WriteLine("    curl " + baseUrl + "/route1      # node 1 or 2");
            Console.WriteLine("    curl " + baseUrl + "/route2      # node 2 or 3");
            Console.WriteLine("    curl " + baseUrl + "/route3      # node 1 or 3");
            Console.WriteLine("    curl -X POST -d 'hi' " + baseUrl + "/echo   # any node, echoes body");
            Console.WriteLine();
            Console.WriteLine("  Repeat a request to see load balancing rotate between eligible nodes.");
            Console.WriteLine("  Press Ctrl+C to stop.");
            Console.WriteLine();
        }

        #endregion
    }
}
