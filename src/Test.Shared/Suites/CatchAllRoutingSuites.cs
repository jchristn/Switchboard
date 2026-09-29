namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using RestWrapper;
    using Switchboard.Core;
    using Switchboard.Core.Database;
    using Test.Shared.Harness;
    using Touchstone.Core;

    /// <summary>
    /// End-to-end catch-all routing: a temporary Switchboard daemon on a random port in front of two
    /// WatsonWebserver origins, configured with catch-all routes ({*name}), a more specific route that
    /// must win over them, a root fallback, a catch-all rewrite, and an invalid pattern. Each case asserts
    /// which origin served the request and exactly what path the origin received.
    /// </summary>
    public static class CatchAllRoutingSuites
    {
        private const string SuiteId = "CatchAllRouting";

        /// <summary>
        /// All catch-all routing suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the catch-all routing suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Catch-All Routing Scenario (precedence, fallback, rewrite, invalid patterns)",
                cases: new List<TestCaseDescriptor>
                {
                    Case("DeepPathReachesOriginIntact", "GET /files/a/b/c.txt is served by the /files catch-all with the full path", async (ctx, ct) =>
                    {
                        CatchAllResponse r = await ctx.SendAsync("GET", "/files/a/b/c.txt", ct).ConfigureAwait(false);
                        Check.Equal(200, r.Status, "status");
                        Check.Equal("Server 1", r.OriginServer, "files endpoint origin");
                        Check.Equal("Hello from Server 1: GET /files/a/b/c.txt", r.Body, "origin saw the original path");
                    }),

                    Case("PrefixItselfMatches", "GET /files (no remainder) is served by the /files catch-all", async (ctx, ct) =>
                    {
                        CatchAllResponse r = await ctx.SendAsync("GET", "/files", ct).ConfigureAwait(false);
                        Check.Equal(200, r.Status, "status");
                        Check.Equal("Server 1", r.OriginServer, "files endpoint origin");
                    }),

                    Case("QueryStringForwarded", "The query string is forwarded unchanged through a catch-all route", async (ctx, ct) =>
                    {
                        CatchAllResponse r = await ctx.SendAsync("GET", "/files/report.csv?page=2&size=10", ct).ConfigureAwait(false);
                        Check.Equal(200, r.Status, "status");
                        Check.Equal("Hello from Server 1: GET /files/report.csv?page=2&size=10", r.Body, "path and query forwarded");
                    }),

                    Case("SpecificRouteBeatsCatchAll", "GET /files/special goes to its own endpoint, not the /files catch-all", async (ctx, ct) =>
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            CatchAllResponse r = await ctx.SendAsync("GET", "/files/special", ct).ConfigureAwait(false);
                            Check.Equal(200, r.Status, "status");
                            Check.Equal("Server 2", r.OriginServer, "specific endpoint origin");
                        }
                    }),

                    Case("LongerCatchAllBeatsShorter", "GET /files/archive/x goes to the longer /files/archive catch-all", async (ctx, ct) =>
                    {
                        CatchAllResponse r = await ctx.SendAsync("GET", "/files/archive/2024/q1.zip", ct).ConfigureAwait(false);
                        Check.Equal(200, r.Status, "status");
                        Check.Equal("Server 2", r.OriginServer, "archive endpoint origin");
                        Check.Equal("Hello from Server 2: GET /files/archive/2024/q1.zip", r.Body, "full path forwarded");
                    }),

                    Case("RootFallbackServesUnmatchedPaths", "A /{*path} fallback serves paths no other route matches", async (ctx, ct) =>
                    {
                        CatchAllResponse r = await ctx.SendAsync("GET", "/anything/else/here", ct).ConfigureAwait(false);
                        Check.Equal(200, r.Status, "status");
                        Check.Equal("Server 2", r.OriginServer, "fallback endpoint origin");
                        Check.Equal("Hello from Server 2: GET /anything/else/here", r.Body, "full path forwarded");
                    }),

                    Case("RootFallbackTakesOverHomepage", "With a /{*path} fallback, GET / is proxied instead of serving the built-in homepage", async (ctx, ct) =>
                    {
                        CatchAllResponse r = await ctx.SendAsync("GET", "/", ct).ConfigureAwait(false);
                        Check.Equal(200, r.Status, "status");
                        Check.False(r.Body.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0, "built-in homepage not served");
                    }),

                    Case("CatchAllRewriteKeepsRemainder", "A /legacy/{*rest} rewrite sends /api/v2/{rest} to the origin", async (ctx, ct) =>
                    {
                        CatchAllResponse r = await ctx.SendAsync("GET", "/legacy/users/5/orders?limit=3", ct).ConfigureAwait(false);
                        Check.Equal(200, r.Status, "status");
                        Check.Equal("Server 1", r.OriginServer, "legacy endpoint origin");
                        Check.Equal("/api/v2/users/5/orders", EchoedField(r.Body, "path"), "rewritten path");
                        Check.Equal("limit=3", EchoedField(r.Body, "query"), "query preserved");
                        Check.False(r.Body.Contains("/legacy/"), "origin did not see the original path");
                    }),

                    Case("CatchAllRewriteKeepsTrailingSlash", "A catch-all rewrite keeps a trailing slash in the remainder", async (ctx, ct) =>
                    {
                        CatchAllResponse r = await ctx.SendAsync("GET", "/legacy/users/", ct).ConfigureAwait(false);
                        Check.Equal(200, r.Status, "status");
                        Check.Equal("/api/v2/users/", EchoedField(r.Body, "path"), "trailing slash preserved");
                    }),

                    Case("InvalidPatternIsIgnored", "An invalid pattern never matches; the request falls through to the fallback", async (ctx, ct) =>
                    {
                        CatchAllResponse r = await ctx.SendAsync("GET", "/broken/a/tail", ct).ConfigureAwait(false);
                        Check.Equal(200, r.Status, "status (not a 500)");
                        Check.Equal("Server 2", r.OriginServer, "served by the fallback, not the invalid route");
                    }),

                    Case("MethodMismatchReturns400", "A GET-only catch-all does not serve POST; with no POST route the gateway returns 400", async (ctx, ct) =>
                    {
                        CatchAllResponse r = await ctx.SendAsync("POST", "/files/a/b", ct).ConfigureAwait(false);
                        Check.Equal(400, r.Status, "no POST route");
                    }),

                    Case("AuthenticatedCatchAllRequiresCredentials", "An authenticated catch-all rejects anonymous requests and serves authorized ones", async (ctx, ct) =>
                    {
                        CatchAllResponse anon = await ctx.SendAsync("GET", "/secure/a/b", ct).ConfigureAwait(false);
                        Check.Equal(401, anon.Status, "anonymous request rejected");

                        Dictionary<string, string> headers = new Dictionary<string, string> { { "Authorization", "Bearer test-token" } };
                        CatchAllResponse authed = await ctx.SendAsync("GET", "/secure/a/b", ct, headers).ConfigureAwait(false);
                        Check.Equal(200, authed.Status, "authorized request served");
                        Check.Equal("Server 1", authed.OriginServer, "secure endpoint origin");
                    })
                });
        }

        // Read a string field from the JSON the origin echoes for /api/ paths.
        private static string EchoedField(string body, string field)
        {
            using (JsonDocument doc = JsonDocument.Parse(body))
            {
                return doc.RootElement.TryGetProperty(field, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;
            }
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<CatchAllContext, CancellationToken, Task> body)
        {
            return new TestCaseDescriptor(
                suiteId: SuiteId,
                caseId: caseId,
                displayName: displayName,
                executeAsync: async ct =>
                {
                    CatchAllContext ctx = await CatchAllContext.CreateAsync(ct).ConfigureAwait(false);
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

        // Response fields the catch-all assertions care about.
        private sealed class CatchAllResponse
        {
            public int Status { get; set; }
            public string Body { get; set; } = string.Empty;
            public string OriginServer { get; set; } = string.Empty;
        }

        // A self-contained scenario: two Watson origins and a Switchboard daemon, each on a random free
        // localhost port, wired up with catch-all routes, a specific route, a fallback, and a rewrite.
        private sealed class CatchAllContext : IDisposable
        {
            private readonly int _ProxyPort;
            private readonly List<OriginHost> _Origins = new List<OriginHost>();
            private readonly string _DbPath;
            private SwitchboardDaemon? _Daemon;
            private bool _Disposed;

            private CatchAllContext(int proxyPort)
            {
                _ProxyPort = proxyPort;
                _DbPath = Path.Combine(Path.GetTempPath(), "switchboard_catchall_" + Guid.NewGuid().ToString("N") + ".db");
            }

            public static async Task<CatchAllContext> CreateAsync(CancellationToken token)
            {
                int proxyPort = FreeTcpPort();
                int p1 = FreeTcpPort();
                int p2 = FreeTcpPort();

                CatchAllContext ctx = new CatchAllContext(proxyPort);
                ctx._Origins.Add(new OriginHost("Server 1", p1));
                ctx._Origins.Add(new OriginHost("Server 2", p2));
                foreach (OriginHost origin in ctx._Origins) origin.Start();

                SwitchboardSettings settings = BuildSettings(proxyPort, ctx._DbPath, p1, p2);
                ctx._Daemon = new SwitchboardDaemon(settings);
                ctx._Daemon.Callbacks.AuthenticateAndAuthorize = AuthCallbacks.AuthenticateAndAuthorize;

                await ctx.WaitForHealthyAsync(settings, TimeSpan.FromSeconds(25), token).ConfigureAwait(false);
                return ctx;
            }

            public async Task<CatchAllResponse> SendAsync(string method, string path, CancellationToken token, IReadOnlyDictionary<string, string>? headers = null)
            {
                System.Net.Http.HttpMethod httpMethod = new System.Net.Http.HttpMethod(method);
                using (RestRequest req = new RestRequest("http://127.0.0.1:" + _ProxyPort + path, httpMethod))
                {
                    if (headers != null)
                    {
                        foreach (KeyValuePair<string, string> kvp in headers)
                        {
                            if (kvp.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                                req.Authorization.BearerToken = kvp.Value.Replace("Bearer ", "");
                            else
                                req.Headers.Add(kvp.Key, kvp.Value);
                        }
                    }

                    using (RestResponse resp = method == "GET" ? await req.SendAsync().ConfigureAwait(false) : await req.SendAsync("{}").ConfigureAwait(false))
                    {
                        CatchAllResponse result = new CatchAllResponse();
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

            private static SwitchboardSettings BuildSettings(int proxyPort, string dbPath, int p1, int p2)
            {
                SwitchboardSettings settings = new SwitchboardSettings();

                settings.Webserver.Hostname = "127.0.0.1";
                settings.Webserver.Port = proxyPort;
                settings.Logging.ConsoleLogging = false;
                settings.Logging.MinimumSeverity = 7;
                settings.Database.Type = DatabaseTypeEnum.Sqlite;
                settings.Database.Filename = dbPath;
                settings.Management.Enable = false;
                settings.RequestHistory.Enable = false;

                settings.Origins.Add(Origin("Server 1", p1));
                settings.Origins.Add(Origin("Server 2", p2));

                // Order matters for the precedence cases: the root fallback and the /files catch-all are
                // configured BEFORE the more specific routes that must still win over them.
                settings.Endpoints.Add(Endpoint("ep-fallback", "GET", new List<string> { "/{*path}" }, "Server 2"));
                settings.Endpoints.Add(Endpoint("ep-files", "GET", new List<string> { "/files/{*path}" }, "Server 1"));
                settings.Endpoints.Add(Endpoint("ep-specific", "GET", new List<string> { "/files/special" }, "Server 2"));
                settings.Endpoints.Add(Endpoint("ep-archive", "GET", new List<string> { "/files/archive/{*path}" }, "Server 2"));

                // An invalid pattern (catch-all not last). It must never match or throw.
                settings.Endpoints.Add(Endpoint("ep-broken", "GET", new List<string> { "/broken/{*rest}/tail" }, "Server 1"));

                // /legacy/{*rest} is rewritten to /api/v2/{rest}; the origin echoes the path it received as JSON.
                ApiEndpoint legacy = Endpoint("ep-legacy", "GET", new List<string> { "/legacy/{*rest}" }, "Server 1");
                legacy.RewriteUrls["GET"] = new Dictionary<string, string> { { "/legacy/{*rest}", "/api/v2/{rest}" } };
                settings.Endpoints.Add(legacy);

                // An authenticated catch-all.
                ApiEndpoint secure = new ApiEndpoint();
                secure.Identifier = "ep-secure";
                secure.Name = "ep-secure";
                secure.LoadBalancing = LoadBalancingMode.RoundRobin;
                secure.Authenticated = new ApiEndpointGroup
                {
                    ParameterizedUrls = new Dictionary<string, List<string>> { { "GET", new List<string> { "/secure/{*rest}" } } }
                };
                secure.OriginServers = new List<string> { "Server 1" };
                settings.Endpoints.Add(secure);

                return settings;
            }

            private static OriginServer Origin(string name, int port)
            {
                OriginServer origin = new OriginServer();
                origin.Identifier = name;
                origin.Name = name;
                origin.Hostname = "127.0.0.1";
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

            private static ApiEndpoint Endpoint(string identifier, string method, List<string> patterns, string origin)
            {
                ApiEndpoint endpoint = new ApiEndpoint();
                endpoint.Identifier = identifier;
                endpoint.Name = identifier;
                endpoint.LoadBalancing = LoadBalancingMode.RoundRobin;
                endpoint.Unauthenticated = new ApiEndpointGroup
                {
                    ParameterizedUrls = new Dictionary<string, List<string>> { { method, patterns } }
                };
                endpoint.OriginServers = new List<string> { origin };
                return endpoint;
            }

            // Ports below the OS ephemeral range, so outbound client sockets cannot claim them before the
            // server binds (see TestPorts).
            private static int FreeTcpPort()
            {
                return TestPorts.Next();
            }
        }
    }
}
