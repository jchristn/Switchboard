namespace Test
{
    using System;
    using System.IO;
    using System.Text;
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
                Ssl = false
            });

            _Settings.Origins.Add(new OriginServer
            {
                Identifier = "server2",
                Name = "Server 2",
                Hostname = "localhost",
                Port = 8002,
                Ssl = false
            });

            _Settings.Origins.Add(new OriginServer
            {
                Identifier = "server3",
                Name = "Server 3",
                Hostname = "localhost",
                Port = 8003,
                Ssl = false
            });

            _Settings.Origins.Add(new OriginServer
            {
                Identifier = "server4",
                Name = "Server 4",
                Hostname = "localhost",
                Port = 8004,
                Ssl = false
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

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}