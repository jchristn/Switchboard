namespace Test
{
    using System;
    using RestWrapper;
    using Switchboard.Core;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    public static class Program
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        private static SwitchboardSettings _Settings = null;
        private static LoadBalancingMode _LoadBalancingMode = LoadBalancingMode.RoundRobin;

        private static WebserverSettings _Server1Settings = null;
        private static Webserver _Server1 = null;

        private static WebserverSettings _Server2Settings = null;
        private static Webserver _Server2 = null;

        private static WebserverSettings _Server3Settings = null;
        private static Webserver _Server3 = null;

        private static int _NumRequests = 30;

        public static async Task Main(string[] args)
        {
            InitializeSettings();
            InitializeOriginServers();

            using (SwitchboardDaemon switchboard = new SwitchboardDaemon(_Settings))
            {
                using (_Server1 = new Webserver(_Server1Settings, Server1Route))
                {
                    using (_Server2 = new Webserver(_Server2Settings, Server2Route))
                    {
                        using (_Server3 = new Webserver(_Server3Settings, Server3Route))
                        {
                            _Server1.Start();
                            _Server2.Start();
                            _Server3.Start();

                            #region Should-Succeed

                            for (int i = 0; i < _NumRequests; i++)
                            {
                                Console.WriteLine("");
                                Console.WriteLine("Request " + i);

                                string url = "http://localhost:8000/test";
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
                            Console.WriteLine("Expecting failure");

                            using (RestRequest req = new RestRequest("http://localhost:8000/undefined"))
                            {
                                using (RestResponse resp = await req.SendAsync())
                                {
                                    Console.WriteLine("| Response (" + resp.StatusCode + "): " + resp.DataAsString);
                                }
                            }

                            #endregion

                            #region URL-Rewrite

                            for (int i = 0; i < _NumRequests; i++)
                            {
                                Console.WriteLine("");
                                Console.WriteLine("URL rewrite request " + i);

                                string url = "http://localhost:8000/users/" + i.ToString();
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
                        }
                    }
                }
            }
        }

        private static void InitializeSettings()
        {
            _Settings = new SwitchboardSettings();
            _Settings.Logging.MinimumSeverity = 1;
            _Settings.Endpoints.Add(new ApiEndpoint
            {
                Identifier = "testendpoint",
                Name = "Test Endpoint",
                LoadBalancing = _LoadBalancingMode,
                ParameterizedUrls = new Dictionary<string, List<string>>
                {
                    { 
                        "GET", 
                        new List<string> 
                        { 
                            "/test",
                            "/users/{UserId}"
                        } 
                    },
                },
                RewriteUrls = new Dictionary<string, Dictionary<string, string>>
                {
                    { 
                        "GET", new Dictionary<string, string>
                        {
                            { "/users/{UserId}", "/{UserId}" }
                        }
                    }
                },
                OriginServers = new List<string>
                {
                    "server1",
                    "server2",
                    "server3"
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
        }

        private static async Task Server1Route(HttpContextBase ctx)
        {
            Console.WriteLine("| Server 1");
            Console.WriteLine("| Received URL: " + ctx.Request.Url.Full);
            if (!String.IsNullOrEmpty(ctx.Request.Query.Querystring)) 
                Console.WriteLine("| Querystring: " + ctx.Request.Query.Querystring);

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hello from server 1");
            return;
        }

        private static async Task Server2Route(HttpContextBase ctx)
        {
            Console.WriteLine("| Server 2");
            Console.WriteLine("| Received URL: " + ctx.Request.Url.Full);
            if (!String.IsNullOrEmpty(ctx.Request.Query.Querystring))
                Console.WriteLine("| Querystring: " + ctx.Request.Query.Querystring);

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hello from server 2");
            return;
        }

        private static async Task Server3Route(HttpContextBase ctx)
        {
            Console.WriteLine("| Server 3");
            Console.WriteLine("| Received URL: " + ctx.Request.Url.Full);
            if (!String.IsNullOrEmpty(ctx.Request.Query.Querystring))
                Console.WriteLine("| Querystring: " + ctx.Request.Query.Querystring);

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hello from server 3");
            return;
        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}