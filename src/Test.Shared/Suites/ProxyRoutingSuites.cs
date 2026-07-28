namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using RestWrapper;
    using Switchboard.Core;
    using Switchboard.Core.Database;
    using Test.Shared.Harness;
    using Touchstone.Core;

    /// <summary>
    /// A dedicated end-to-end proxy scenario: a temporary Switchboard daemon on a random port in front
    /// of three WatsonWebserver origins (also on random ports), configured with several endpoints that
    /// route different URLs to different origin subsets, plus a URL rewrite and a global blocked
    /// header. Each case asserts the exact result, proving per-route origin selection, round-robin
    /// balancing within a subset, URL rewriting, and blocked-header stripping all behave together.
    /// </summary>
    public static class ProxyRoutingSuites
    {
        /// <summary>
        /// All routing scenario suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the routing scenario suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ProxyRouting",
                displayName: "Proxy Routing Scenario (subset routing, rewrite, blocked headers)",
                cases: new List<TestCaseDescriptor>
                {
                    Case("PairRouteHitsOnlyItsSubset", "GET /pair is served only by Server 1 or Server 2", async (ctx, ct) =>
                    {
                        HashSet<string> seen = new HashSet<string>();
                        for (int i = 0; i < 12; i++)
                        {
                            RoutingResponse r = await ctx.GetAsync("/pair", ct).ConfigureAwait(false);
                            Check.Equal(200, r.Status, "pair status");
                            Check.True(r.OriginServer == "Server 1" || r.OriginServer == "Server 2",
                                "pair served by Server 1 or 2 (got " + r.OriginServer + ")");
                            seen.Add(r.OriginServer);
                        }
                        Check.True(seen.Contains("Server 1") && seen.Contains("Server 2"), "round-robin used both Server 1 and 2");
                        Check.False(seen.Contains("Server 3"), "Server 3 never served /pair");
                    }),

                    Case("SoloRouteHitsOnlyOneOrigin", "GET /solo is served only by Server 3", async (ctx, ct) =>
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            RoutingResponse r = await ctx.GetAsync("/solo", ct).ConfigureAwait(false);
                            Check.Equal(200, r.Status, "solo status");
                            Check.Equal("Server 3", r.OriginServer, "solo always served by Server 3");
                        }
                    }),

                    Case("AllRouteBalancesAcrossEveryOrigin", "GET /all rotates across all three origins", async (ctx, ct) =>
                    {
                        HashSet<string> seen = new HashSet<string>();
                        for (int i = 0; i < 15; i++)
                        {
                            RoutingResponse r = await ctx.GetAsync("/all", ct).ConfigureAwait(false);
                            Check.Equal(200, r.Status, "all status");
                            seen.Add(r.OriginServer);
                        }
                        Check.True(seen.Contains("Server 1") && seen.Contains("Server 2") && seen.Contains("Server 3"),
                            "all three origins served /all (saw " + String.Join(",", seen) + ")");
                    }),

                    Case("ExactBodyFromOrigin", "The proxied response body is exactly what the origin returned", async (ctx, ct) =>
                    {
                        RoutingResponse r = await ctx.GetAsync("/solo", ct).ConfigureAwait(false);
                        Check.Equal(200, r.Status, "status");
                        Check.Equal("Hello from Server 3: GET /solo", r.Body, "exact echoed body");
                    }),

                    Case("UrlRewriteRewritesPathToOrigin", "A configured rewrite changes the path the origin receives", async (ctx, ct) =>
                    {
                        RoutingResponse r = await ctx.GetAsync("/api/v2/users/42", ct).ConfigureAwait(false);
                        Check.Equal(200, r.Status, "rewrite status");
                        Check.Contains(r.Body, "/api/v1/users/42", "origin saw the rewritten path");
                        Check.False(r.Body.Contains("/api/v2/"), "origin did not see the original path");
                    }),

                    Case("BlockedHeaderIsStripped", "A globally blocked header is removed before reaching the origin", async (ctx, ct) =>
                    {
                        Dictionary<string, string> headers = new Dictionary<string, string>
                        {
                            { "X-Blocked-Secret", "must-not-pass" },
                            { "X-Allowed-Marker", "should-pass" }
                        };
                        RoutingResponse r = await ctx.GetAsync("/api/echo", ct, headers).ConfigureAwait(false);
                        Check.Equal(200, r.Status, "echo status");
                        Check.Contains(r.Body, "should-pass", "allowed header reached the origin");
                        Check.False(r.Body.Contains("must-not-pass"), "blocked header was stripped");
                    }),

                    Case("UnconfiguredPathReturns400", "A path with no configured route returns 400", async (ctx, ct) =>
                    {
                        RoutingResponse r = await ctx.GetAsync("/nothing-here", ct).ConfigureAwait(false);
                        Check.Equal(400, r.Status, "unconfigured path status");
                    })
                });
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<RoutingContext, CancellationToken, Task> body)
        {
            return new TestCaseDescriptor(
                suiteId: "ProxyRouting",
                caseId: caseId,
                displayName: displayName,
                executeAsync: async ct =>
                {
                    RoutingContext ctx = await RoutingContext.CreateAsync(ct).ConfigureAwait(false);
                    try
                    {
                        await body(ctx, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        ctx.Dispose();
                    }
                });
        }

        // Response fields the routing assertions care about.
        private sealed class RoutingResponse
        {
            public int Status { get; set; }
            public string Body { get; set; } = string.Empty;
            public string OriginServer { get; set; } = string.Empty;
        }

        // A self-contained scenario: three Watson origins and a Switchboard daemon, each on a random
        // free localhost port, wired up with subset-routing endpoints, a rewrite, and a blocked header.
        private sealed class RoutingContext : IDisposable
        {
            private readonly int _ProxyPort;
            private readonly List<OriginHost> _Origins = new List<OriginHost>();
            private readonly string _DbPath;
            private SwitchboardDaemon? _Daemon;
            private bool _Disposed;

            private RoutingContext(int proxyPort)
            {
                _ProxyPort = proxyPort;
                _DbPath = Path.Combine(Path.GetTempPath(), "switchboard_routing_" + Guid.NewGuid().ToString("N") + ".db");
            }

            public static async Task<RoutingContext> CreateAsync(CancellationToken token)
            {
                int proxyPort = FreeTcpPort();
                int p1 = FreeTcpPort();
                int p2 = FreeTcpPort();
                int p3 = FreeTcpPort();

                RoutingContext ctx = new RoutingContext(proxyPort);
                ctx._Origins.Add(new OriginHost("Server 1", p1));
                ctx._Origins.Add(new OriginHost("Server 2", p2));
                ctx._Origins.Add(new OriginHost("Server 3", p3));
                foreach (OriginHost origin in ctx._Origins) origin.Start();

                SwitchboardSettings settings = BuildSettings(proxyPort, ctx._DbPath, p1, p2, p3);
                ctx._Daemon = new SwitchboardDaemon(settings);

                await ctx.WaitForHealthyAsync(settings, TimeSpan.FromSeconds(25), token).ConfigureAwait(false);
                return ctx;
            }

            public string Url(string pathAndQuery)
            {
                return "http://localhost:" + _ProxyPort + pathAndQuery;
            }

            public async Task<RoutingResponse> GetAsync(string path, CancellationToken token, IReadOnlyDictionary<string, string>? headers = null)
            {
                using (RestRequest req = new RestRequest(Url(path)))
                {
                    if (headers != null)
                        foreach (KeyValuePair<string, string> kvp in headers)
                            req.Headers.Add(kvp.Key, kvp.Value);

                    using (RestResponse resp = await req.SendAsync().ConfigureAwait(false))
                    {
                        RoutingResponse result = new RoutingResponse();
                        result.Status = resp.StatusCode;
                        result.Body = resp.DataAsString ?? string.Empty;
                        if (resp.Headers != null) result.OriginServer = resp.Headers.Get("X-Origin-Server") ?? string.Empty;
                        return result;
                    }
                }
            }

            public void Dispose()
            {
                if (_Disposed) return;
                _Disposed = true;

                try { _Daemon?.Dispose(); } catch (Exception) { }
                foreach (OriginHost origin in _Origins)
                {
                    try { origin.Dispose(); } catch (Exception) { }
                }
                try { if (File.Exists(_DbPath)) File.Delete(_DbPath); } catch (Exception) { }
            }

            private async Task WaitForHealthyAsync(SwitchboardSettings settings, TimeSpan timeout, CancellationToken token)
            {
                DateTime deadline = DateTime.UtcNow.Add(timeout);
                while (settings.Origins.Any(o => !o.Healthy))
                {
                    if (DateTime.UtcNow > deadline) throw new TimeoutException("Origins did not become healthy in time.");
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
            }

            private static SwitchboardSettings BuildSettings(int proxyPort, string dbPath, int p1, int p2, int p3)
            {
                SwitchboardSettings settings = new SwitchboardSettings();

                settings.Webserver.Hostname = "localhost";
                settings.Webserver.Port = proxyPort;
                settings.Logging.ConsoleLogging = false;
                settings.Logging.MinimumSeverity = 7;
                settings.Database.Type = DatabaseTypeEnum.Sqlite;
                settings.Database.Filename = dbPath;
                settings.Management.Enable = false;
                settings.RequestHistory.Enable = false;
                settings.BlockedHeaders.Add("x-blocked-secret");

                settings.Origins.Add(Origin("Server 1", p1));
                settings.Origins.Add(Origin("Server 2", p2));
                settings.Origins.Add(Origin("Server 3", p3));

                // /all and /api/echo -> all three origins.
                settings.Endpoints.Add(Endpoint("ep-all", new Dictionary<string, List<string>>
                {
                    { "GET", new List<string> { "/all", "/api/echo" } }
                }, new List<string> { "Server 1", "Server 2", "Server 3" }, null));

                // /pair -> only Server 1 and Server 2.
                settings.Endpoints.Add(Endpoint("ep-pair", new Dictionary<string, List<string>>
                {
                    { "GET", new List<string> { "/pair" } }
                }, new List<string> { "Server 1", "Server 2" }, null));

                // /solo -> only Server 3.
                settings.Endpoints.Add(Endpoint("ep-solo", new Dictionary<string, List<string>>
                {
                    { "GET", new List<string> { "/solo" } }
                }, new List<string> { "Server 3" }, null));

                // /api/v2/users/{userId} -> Server 1, rewritten to /api/v1/users/{userId}.
                Dictionary<string, Dictionary<string, string>> rewrite = new Dictionary<string, Dictionary<string, string>>
                {
                    { "GET", new Dictionary<string, string> { { "/api/v2/users/{userId}", "/api/v1/users/{userId}" } } }
                };
                settings.Endpoints.Add(Endpoint("ep-rewrite", new Dictionary<string, List<string>>
                {
                    { "GET", new List<string> { "/api/v2/users/{userId}" } }
                }, new List<string> { "Server 1" }, rewrite));

                return settings;
            }

            private static OriginServer Origin(string name, int port)
            {
                OriginServer origin = new OriginServer();
                origin.Identifier = name;
                origin.Name = name;
                origin.Hostname = "localhost";
                origin.Port = port;
                origin.Ssl = false;
                origin.HealthCheckUrl = "/";
                origin.HealthCheckIntervalMs = 1000;
                origin.HealthyThreshold = 1;
                origin.UnhealthyThreshold = 2;
                origin.MaxParallelRequests = 100;
                origin.RateLimitRequestsThreshold = 1000;
                return origin;
            }

            private static ApiEndpoint Endpoint(
                string identifier,
                Dictionary<string, List<string>> parameterizedUrls,
                List<string> originIdentifiers,
                Dictionary<string, Dictionary<string, string>>? rewriteUrls)
            {
                ApiEndpoint endpoint = new ApiEndpoint();
                endpoint.Identifier = identifier;
                endpoint.Name = identifier;
                endpoint.LoadBalancing = LoadBalancingMode.RoundRobin;
                endpoint.Unauthenticated = new ApiEndpointGroup { ParameterizedUrls = parameterizedUrls };
                endpoint.OriginServers = originIdentifiers;
                if (rewriteUrls != null) endpoint.RewriteUrls = rewriteUrls;
                return endpoint;
            }

            private static int FreeTcpPort()
            {
                TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                try
                {
                    return ((IPEndPoint)listener.LocalEndpoint).Port;
                }
                finally
                {
                    listener.Stop();
                }
            }
        }
    }
}
