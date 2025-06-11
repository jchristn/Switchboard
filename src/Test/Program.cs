namespace Test
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using RestWrapper;
    using SerializationHelper;
    using Switchboard.Core;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    public static class Program
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

        private static SwitchboardSettings _Settings = null;
        private static Serializer _Serializer = new Serializer();
        private static LoadBalancingMode _LoadBalancingMode = LoadBalancingMode.RoundRobin;

        private static WebserverSettings _Server1Settings = null;
        private static Webserver _Server1 = null;

        private static WebserverSettings _Server2Settings = null;
        private static Webserver _Server2 = null;

        private static WebserverSettings _Server3Settings = null;
        private static Webserver _Server3 = null;

        private static WebserverSettings _Server4Settings = null;
        private static Webserver _Server4 = null;

        private static int _NumRequests = 8;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("");
            Console.WriteLine("Unauthenticated URL : GET /unauthenticated");
            Console.WriteLine("Authenticated URL   : GET /authenticated");
            InitializeSettings();
            InitializeOriginServers();

            string url;

            using (SwitchboardDaemon switchboard = new SwitchboardDaemon(_Settings))
            {
                switchboard.Callbacks.AuthenticateAndAuthorize = AuthenticateAndAuthorizeRequest;

                using (_Server1 = new Webserver(_Server1Settings, Server1Route))
                {
                    using (_Server2 = new Webserver(_Server2Settings, Server2Route))
                    {
                        using (_Server3 = new Webserver(_Server3Settings, Server3Route))
                        {
                            using (_Server4 = new Webserver(_Server4Settings, Server4Route))
                            {
                                _Server1.Start();
                                _Server2.Start();
                                _Server3.Start();
                                _Server4.Start();

                                #region Wait-for-Healthy-Origins

                                Console.WriteLine("-----------------------------------");
                                Console.WriteLine("Waiting for origin servers to start");
                                Console.WriteLine("-----------------------------------");

                                while (_Settings.Origins.Any(o => !o.Healthy))
                                {
                                    Console.WriteLine("At least one origin is unhealthy, waiting 1000ms");
                                    await Task.Delay(1000);
                                }

                                #endregion

                                #region Should-Succeed

                                Console.WriteLine("");
                                Console.WriteLine("-----------------------------");
                                Console.WriteLine("Unauthenticated success tests");
                                Console.WriteLine("-----------------------------");

                                for (int i = 0; i < _NumRequests; i++)
                                {
                                    Console.WriteLine("");
                                    Console.WriteLine("Request " + i);

                                    url = "http://localhost:8000/unauthenticated";
                                    if (i % 2 == 0) url += "?foo=bar";
                                    Console.WriteLine("| URL: " + url);

                                    using (RestRequest req = new RestRequest(url))
                                    {
                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            Console.WriteLine("| Response (" + resp.StatusCode + "): " + resp.DataAsString);
                                        }
                                    }
                                }

                                #endregion

                                #region Should-Fail

                                Console.WriteLine("");
                                Console.WriteLine("----------------------");
                                Console.WriteLine("Failure test (bad URL)");
                                Console.WriteLine("----------------------");

                                Console.WriteLine("");
                                Console.WriteLine("Expecting failure due to bad URL");

                                using (RestRequest req = new RestRequest("http://localhost:8000/undefined"))
                                {
                                    using (RestResponse resp = await req.SendAsync())
                                    {
                                        Console.WriteLine("| Response (" + resp.StatusCode + "): " + resp.DataAsString);
                                    }
                                }

                                #endregion

                                #region URL-Rewrite

                                Console.WriteLine("");
                                Console.WriteLine("----------------------------------------------");
                                Console.WriteLine("Authenticated requests test (half should fail)");
                                Console.WriteLine("----------------------------------------------");

                                for (int i = 0; i < _NumRequests; i++)
                                {
                                    Console.WriteLine("");
                                    Console.Write("---" + Environment.NewLine + "Authenticated request " + i);
                                    if (i % 2 == 0) Console.WriteLine(": should succeed" + Environment.NewLine + "---");
                                    else Console.WriteLine(": should fail" + Environment.NewLine + "---");

                                    url = "http://localhost:8000/authenticated";
                                    if (i % 2 == 0) url += "?foo=bar";
                                    Console.WriteLine("| URL: " + url);

                                    using (RestRequest req = new RestRequest(url))
                                    {
                                        if (i % 2 == 0) req.Authorization.BearerToken = "foo";

                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            Console.WriteLine("| Response (" + resp.StatusCode + "): " + resp.DataAsString);
                                        }
                                    }
                                }

                                #endregion

                                #region Stop-All-Servers

                                Console.WriteLine("");
                                Console.WriteLine("------------------------------------------------");
                                Console.WriteLine("Health Check Test: All servers down (502 errors)");
                                Console.WriteLine("------------------------------------------------");

                                // Stop all servers
                                Console.WriteLine("");
                                Console.WriteLine("Stopping all origin servers...");
                                _Server1.Stop();
                                _Server2.Stop();
                                _Server3.Stop();
                                _Server4.Stop();

                                Console.WriteLine("----------------------------------");
                                Console.WriteLine("Waiting for origin servers to stop");
                                Console.WriteLine("----------------------------------");

                                while (_Settings.Origins.Any(o => o.Healthy))
                                {
                                    Console.WriteLine("At least one origin is healthy, waiting 1000ms");
                                    await Task.Delay(1000);
                                }

                                #endregion

                                #region Validate-502s

                                // Test that we get 502 errors
                                Console.WriteLine("");
                                Console.WriteLine("Testing requests while all servers are down (expecting 502 errors):");

                                for (int i = 0; i < 3; i++)
                                {
                                    Console.WriteLine("");
                                    Console.WriteLine("Request " + i + " (expecting 502):");

                                    url = "http://localhost:8000/unauthenticated";
                                    Console.WriteLine("| URL: " + url);

                                    using (RestRequest req = new RestRequest(url))
                                    {
                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            Console.WriteLine("| Response (" + resp.StatusCode + "): " + resp.DataAsString);

                                            if (resp.StatusCode != 502)
                                            {
                                                Console.WriteLine("| ERROR: Expected 502 but got " + resp.StatusCode);
                                            }
                                            else
                                            {
                                                Console.WriteLine("| SUCCESS: Received expected 502 error");
                                            }
                                        }
                                    }
                                }

                                #endregion

                                #region Health-Check-Server-Recovery

                                Console.WriteLine("");
                                Console.WriteLine("-------------------------------------------");
                                Console.WriteLine("Health Check Test: Server recovery");
                                Console.WriteLine("-------------------------------------------");

                                // Start server 1 and 2
                                Console.WriteLine("");
                                Console.WriteLine("Starting servers 1 and 2...");
                                _Server1.Start();
                                _Server2.Start();

                                // Wait for health checks to detect recovery
                                // With HealthyThreshold=2 and HealthCheckIntervalMs=1000, we need at least 2 seconds

                                Console.WriteLine("-----------------------------------");
                                Console.WriteLine("Waiting for origin servers to start");
                                Console.WriteLine("-----------------------------------");

                                while (_Settings.Origins.All(o => !o.Healthy))
                                {
                                    Console.WriteLine("All origin servers are unhealthy, waiting 1000ms");
                                    await Task.Delay(1000);
                                }

                                // Test that requests now succeed
                                Console.WriteLine("");
                                Console.WriteLine("Testing requests after server recovery:");

                                for (int i = 0; i < 4; i++)
                                {
                                    Console.WriteLine("");
                                    Console.WriteLine("Request " + i + " (expecting success):");

                                    url = "http://localhost:8000/unauthenticated";
                                    Console.WriteLine("| URL: " + url);

                                    using (RestRequest req = new RestRequest(url))
                                    {
                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            Console.WriteLine("| Response (" + resp.StatusCode + "): " + resp.DataAsString);

                                            if (resp.StatusCode != 200)
                                            {
                                                Console.WriteLine("| ERROR: Expected 200 but got " + resp.StatusCode);
                                            }
                                            else
                                            {
                                                Console.WriteLine("| SUCCESS: Request succeeded after recovery");
                                            }
                                        }
                                    }
                                }

                                // Start remaining servers
                                Console.WriteLine("");
                                Console.WriteLine("Starting remaining servers...");
                                _Server3.Start();
                                _Server4.Start();

                                Console.WriteLine("-----------------------------------");
                                Console.WriteLine("Waiting for origin servers to start");
                                Console.WriteLine("-----------------------------------");

                                while (_Settings.Origins.Any(o => !o.Healthy))
                                {
                                    Console.WriteLine("At least one origin server is unhealthy, waiting 1000ms");
                                    await Task.Delay(1000);
                                }

                                await Task.Delay(3000);

                                #endregion

                                #region Rate-Limiting-Test

                                Console.WriteLine("");
                                Console.WriteLine("-------------------------------------------");
                                Console.WriteLine("Rate Limiting Test");
                                Console.WriteLine("-------------------------------------------");

                                // First, let's ensure all servers are healthy
                                Console.WriteLine("Ensuring all servers are healthy...");
                                await Task.Delay(2000);

                                // Send many concurrent requests to trigger rate limiting
                                Console.WriteLine("");
                                Console.WriteLine("Sending burst of requests to trigger rate limiting...");

                                Task<(int statusCode, string response)>[] tasks = new Task<(int, string)>[20];

                                for (int i = 0; i < tasks.Length; i++)
                                {
                                    int index = i;
                                    tasks[i] = Task.Run(async () =>
                                    {
                                        using (RestRequest req = new RestRequest("http://localhost:8000/unauthenticated"))
                                        {
                                            // Add a small delay to simulate more realistic concurrent requests
                                            if (index > 0) await Task.Delay(10);

                                            using (RestResponse resp = await req.SendAsync())
                                            {
                                                return (resp.StatusCode, resp.DataAsString);
                                            }
                                        }
                                    });
                                }

                                var results = await Task.WhenAll(tasks);

                                int successCount = 0;
                                int rateLimitCount = 0;

                                Console.WriteLine("");
                                Console.WriteLine("Rate limiting results:");
                                for (int i = 0; i < results.Length; i++)
                                {
                                    Console.WriteLine($"| Request {i}: Status {results[i].statusCode}");

                                    if (results[i].statusCode == 200)
                                        successCount++;
                                    else if (results[i].statusCode == 429)
                                        rateLimitCount++;
                                }

                                Console.WriteLine("");
                                Console.WriteLine($"| Summary: {successCount} successful, {rateLimitCount} rate limited");

                                if (rateLimitCount > 0)
                                {
                                    Console.WriteLine("| SUCCESS: Rate limiting is working correctly");
                                }
                                else
                                {
                                    Console.WriteLine("| WARNING: No rate limiting observed (might need to adjust thresholds)");
                                }

                                // Wait for rate limit to clear
                                Console.WriteLine("");
                                Console.WriteLine("Waiting for rate limit to clear...");
                                await Task.Delay(3000);

                                #endregion

                                #region Health-Check-During-Active-Requests

                                Console.WriteLine("");
                                Console.WriteLine("-------------------------------------------");
                                Console.WriteLine("Health Check Test: Server failure during active requests");
                                Console.WriteLine("-------------------------------------------");

                                // Start a long-running request task
                                Console.WriteLine("");
                                Console.WriteLine("Starting requests while servers will be stopped...");

                                Task<(int count, int failures)> requestTask = Task.Run(async () =>
                                {
                                    int requestCount = 0;
                                    int failureCount = 0;

                                    for (int i = 0; i < 10; i++)
                                    {
                                        requestCount++;

                                        using (RestRequest req = new RestRequest("http://localhost:8000/unauthenticated"))
                                        {
                                            using (RestResponse resp = await req.SendAsync())
                                            {
                                                if (resp.StatusCode != 200)
                                                {
                                                    failureCount++;
                                                    Console.WriteLine($"| Request {i}: Failed with status {resp.StatusCode}");
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"| Request {i}: Success from {resp.Headers.Get("X-Origin-Server")}");
                                                }
                                            }
                                        }

                                        await Task.Delay(500);
                                    }

                                    return (requestCount, failureCount);
                                });

                                // Wait a bit then stop some servers
                                await Task.Delay(1000);
                                Console.WriteLine("");
                                Console.WriteLine("Stopping servers 3 and 4 during active requests...");
                                _Server3.Stop();
                                _Server4.Stop();

                                var (totalRequests, failures) = await requestTask;

                                Console.WriteLine("");
                                Console.WriteLine($"| Results: {totalRequests} total requests, {failures} failures");
                                Console.WriteLine("| Remaining healthy servers should have handled the load");

                                // Restart servers
                                Console.WriteLine("");
                                Console.WriteLine("Restarting servers 3 and 4...");
                                _Server3.Start();
                                _Server4.Start();
                                await Task.Delay(3000);

                                #endregion

                                Console.WriteLine("");
                                Console.WriteLine("Press ENTER to end");
                                Console.ReadLine();
                            }
                        }
                    }
                }
            }
        }

        private static async Task<AuthContext> AuthenticateAndAuthorizeRequest(HttpContextBase ctx)
        {
            string authHeader = ctx.Request.RetrieveHeaderValue("Authorization");
            if (!String.IsNullOrEmpty(authHeader))
            {
                return new AuthContext
                {
                    Authentication = new AuthenticationContext
                    {
                        Result = AuthenticationResultEnum.Success,
                        Metadata = new Dictionary<string, string>()
                        {
                            { "Authenticated", "true" }
                        }
                    },
                    Authorization = new AuthorizationContext
                    {
                        Result = AuthorizationResultEnum.Success,
                        Metadata = new Dictionary<string, string>()
                        {
                            { "Authorized", "true" }
                        }
                    },
                    Metadata = new Dictionary<string, string>()
                    {
                        { "Allow", "true" }
                    }
                };
            }
            else
            {
                return new AuthContext
                {
                    Authentication = new AuthenticationContext
                    {
                        Result = AuthenticationResultEnum.Denied,
                        Metadata = new Dictionary<string, string>()
                        {
                            { "Authenticated", "false" }
                        }
                    },
                    Authorization = new AuthorizationContext
                    {
                        Result = AuthorizationResultEnum.Denied,
                        Metadata = new Dictionary<string, string>()
                        {
                            { "Authorized", "false" }
                        }
                    },
                    Metadata = new Dictionary<string, string>()
                    {
                        { "Allow", "false" },
                        { "Error", "Supply an Authorization header in your request" }
                    },
                    FailureMessage = "Supply an Authorization header in your request"
                };
            }
        }

        private static void InitializeSettings()
        {
            _Settings = new SwitchboardSettings();
            _Settings.Logging.MinimumSeverity = 1;

            _Settings.Endpoints.Add(new ApiEndpoint
            {
                Identifier = "test-endpoint",
                Name = "Test Endpoint",
                LoadBalancing = _LoadBalancingMode,
                AuthContextHeader = Constants.AuthContextHeader,
                Unauthenticated = new ApiEndpointGroup
                {
                    ParameterizedUrls = new Dictionary<string, List<string>>
                    {
                        {
                            "GET", new List<string> { "/unauthenticated" }
                        }
                    }
                },
                Authenticated = new ApiEndpointGroup
                {
                    ParameterizedUrls = new Dictionary<string, List<string>>
                    {
                        {
                            "GET", new List<string> { "/authenticated" }
                        }
                    }
                },
                OriginServers = new List<string>
                {
                    "server1",
                    "server2",
                    "server3",
                    "server4"
                }
            });

            _Settings.Origins.Add(new OriginServer
            {
                Identifier = "server1",
                Name = "Server 1",
                Hostname = "localhost",
                Port = 8001,
                Ssl = false,
                HealthCheckIntervalMs = 1000,  // Check every second for faster testing
                UnhealthyThreshold = 2,        // 2 failures = unhealthy
                HealthyThreshold = 2,          // 2 successes = healthy
                MaxParallelRequests = 2,       // Low limit for rate limiting tests
                RateLimitRequestsThreshold = 3 // Very low threshold for testing
            });

            _Settings.Origins.Add(new OriginServer
            {
                Identifier = "server2",
                Name = "Server 2",
                Hostname = "localhost",
                Port = 8002,
                Ssl = false,
                HealthCheckIntervalMs = 1000,
                UnhealthyThreshold = 2,
                HealthyThreshold = 2,
                MaxParallelRequests = 2,
                RateLimitRequestsThreshold = 3
            });

            _Settings.Origins.Add(new OriginServer
            {
                Identifier = "server3",
                Name = "Server 3",
                Hostname = "localhost",
                Port = 8003,
                Ssl = false,
                HealthCheckIntervalMs = 1000,
                UnhealthyThreshold = 2,
                HealthyThreshold = 2,
                MaxParallelRequests = 2,
                RateLimitRequestsThreshold = 3
            });

            _Settings.Origins.Add(new OriginServer
            {
                Identifier = "server4",
                Name = "Server 4",
                Hostname = "localhost",
                Port = 8004,
                Ssl = false,
                HealthCheckIntervalMs = 1000,
                UnhealthyThreshold = 2,
                HealthyThreshold = 2,
                MaxParallelRequests = 2,
                RateLimitRequestsThreshold = 3
            });

            File.WriteAllBytes("./sb.json", Encoding.UTF8.GetBytes(_Serializer.SerializeJson(_Settings, true)));
        }

        private static void InitializeOriginServers()
        {
            _Server1Settings = new WebserverSettings();
            _Server1Settings.Hostname = "localhost";
            _Server1Settings.Port = 8001;

            _Server2Settings = new WebserverSettings();
            _Server2Settings.Hostname = "localhost";
            _Server2Settings.Port = 8002;

            _Server3Settings = new WebserverSettings();
            _Server3Settings.Hostname = "localhost";
            _Server3Settings.Port = 8003;

            _Server4Settings = new WebserverSettings();
            _Server4Settings.Hostname = "localhost";
            _Server4Settings.Port = 8004;
        }

        private static async Task Server1Route(HttpContextBase ctx)
        {
            if (IsHealthCheck(ctx))
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send();
                return;
            }

            Console.WriteLine("| Server 1");
            Console.WriteLine("| Received URL: " + ctx.Request.Url.Full);
            if (!String.IsNullOrEmpty(ctx.Request.Query.Querystring))
                Console.WriteLine("| Querystring: " + ctx.Request.Query.Querystring);
            if (ctx.Request.Headers.Count > 0)
            {
                Console.WriteLine("| Headers:");
                foreach (string key in ctx.Request.Headers.AllKeys)
                {
                    Console.WriteLine("  | " + key + ": " + ctx.Request.Headers.Get(key));
                }

                if (ctx.Request.Headers.AllKeys.Contains(Constants.AuthContextHeader))
                {
                    Console.WriteLine(
                        "| Auth context: " + Environment.NewLine
                        + _Serializer.SerializeJson(
                            AuthContext.FromBase64String(ctx.Request.Headers.Get(Constants.AuthContextHeader)),
                            true));
                }
            }
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hello from server 1: " + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery);
            return;
        }

        private static async Task Server2Route(HttpContextBase ctx)
        {
            if (IsHealthCheck(ctx))
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send();
                return;
            }

            Console.WriteLine("| Server 2");
            Console.WriteLine("| Received URL: " + ctx.Request.Url.Full);
            if (!String.IsNullOrEmpty(ctx.Request.Query.Querystring))
                Console.WriteLine("| Querystring: " + ctx.Request.Query.Querystring);
            if (ctx.Request.Headers.Count > 0)
            {
                Console.WriteLine("| Headers:");
                foreach (string key in ctx.Request.Headers.AllKeys)
                {
                    Console.WriteLine("  | " + key + ": " + ctx.Request.Headers.Get(key));
                }

                if (ctx.Request.Headers.AllKeys.Contains(Constants.AuthContextHeader))
                {
                    Console.WriteLine(
                        "| Auth context: " + Environment.NewLine
                        + _Serializer.SerializeJson(
                            AuthContext.FromBase64String(ctx.Request.Headers.Get(Constants.AuthContextHeader)),
                            true));
                }
            }
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hello from server 2: " + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery);
            return;
        }

        private static async Task Server3Route(HttpContextBase ctx)
        {
            if (IsHealthCheck(ctx))
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send();
                return;
            }

            Console.WriteLine("| Server 3");
            Console.WriteLine("| Received URL: " + ctx.Request.Url.Full);
            if (!String.IsNullOrEmpty(ctx.Request.Query.Querystring))
                Console.WriteLine("| Querystring: " + ctx.Request.Query.Querystring);
            if (ctx.Request.Headers.Count > 0)
            {
                Console.WriteLine("| Headers:");
                foreach (string key in ctx.Request.Headers.AllKeys)
                {
                    Console.WriteLine("  | " + key + ": " + ctx.Request.Headers.Get(key));
                }

                if (ctx.Request.Headers.AllKeys.Contains(Constants.AuthContextHeader))
                {
                    Console.WriteLine(
                        "| Auth context: " + Environment.NewLine
                        + _Serializer.SerializeJson(
                            AuthContext.FromBase64String(ctx.Request.Headers.Get(Constants.AuthContextHeader)),
                            true));
                }
            }
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hello from server 3: " + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery);
            return;
        }

        private static async Task Server4Route(HttpContextBase ctx)
        {
            if (IsHealthCheck(ctx))
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send();
                return;
            }

            Console.WriteLine("| Server 4");
            Console.WriteLine("| Received URL: " + ctx.Request.Url.Full);
            if (!String.IsNullOrEmpty(ctx.Request.Query.Querystring))
                Console.WriteLine("| Querystring: " + ctx.Request.Query.Querystring);
            if (ctx.Request.Headers.Count > 0)
            {
                Console.WriteLine("| Headers:");
                foreach (string key in ctx.Request.Headers.AllKeys)
                {
                    Console.WriteLine("  | " + key + ": " + ctx.Request.Headers.Get(key));
                }

                if (ctx.Request.Headers.AllKeys.Contains(Constants.AuthContextHeader))
                {
                    Console.WriteLine(
                        "| Auth context: " + Environment.NewLine
                        + _Serializer.SerializeJson(
                            AuthContext.FromBase64String(ctx.Request.Headers.Get(Constants.AuthContextHeader)),
                            true));
                }
            }
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hello from server 4: " + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery);
            return;
        }

        private static bool IsHealthCheck(HttpContextBase ctx)
        {
            return (ctx.Request.Method == HttpMethod.GET && ctx.Request.Url.RawWithoutQuery.Equals("/"));
        }

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}