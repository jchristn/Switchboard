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

        private static WebserverSettings _Server4Settings = null;
        private static Webserver _Server4 = null;

        private static int _NumRequests = 10;

        public static async Task Main(string[] args)
        {
            InitializeSettings();
            InitializeOriginServers();

            string url;

            using (SwitchboardDaemon switchboard = new SwitchboardDaemon(_Settings))
            {
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
                                Console.WriteLine("-------------");
                                Console.WriteLine("Success tests");
                                Console.WriteLine("-------------");

                                for (int i = 0; i < _NumRequests; i++)
                                {
                                    Console.WriteLine("");
                                    Console.WriteLine("Request " + i);

                                    url = "http://localhost:8000/test";
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

                                Console.WriteLine("");
                                Console.WriteLine("----------------");
                                Console.WriteLine("URL rewrite test");
                                Console.WriteLine("----------------");

                                for (int i = 0; i < _NumRequests; i++)
                                {
                                    Console.WriteLine("");
                                    Console.WriteLine("URL rewrite request " + i);

                                    url = "http://localhost:8000/users/" + i.ToString();
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

                                #region Server-Sent-Events

                                Console.WriteLine("");
                                Console.WriteLine("-----------------------");
                                Console.WriteLine("Server-sent events test");
                                Console.WriteLine("-----------------------");

                                url = "http://localhost:8000/sse";

                                using (RestRequest req = new RestRequest(url))
                                {
                                    using (RestResponse resp = await req.SendAsync())
                                    {
                                        if (resp.ServerSentEvents)
                                        {
                                            Console.WriteLine("| Using server-sent events");

                                            while (true)
                                            {
                                                ServerSentEvent sse = await resp.ReadEventAsync();
                                                if (sse == null) break;
                                                else
                                                {
                                                    string data = sse.Data;
                                                    if (!String.IsNullOrEmpty(data)) data = data.Trim();

                                                    if (!String.IsNullOrEmpty(data))
                                                    {
                                                        Console.WriteLine("| Event: " + data);
                                                    }
                                                    else
                                                    {
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        else
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

            _Settings.Endpoints.Add(new ApiEndpoint
            {
                Identifier = "sse-endpoint",
                Name = "SSE Endpoint",
                LoadBalancing = _LoadBalancingMode,
                ParameterizedUrls = new Dictionary<string, List<string>>
                {
                    {
                        "GET",
                        new List<string>
                        {
                            "/sse"
                        }
                    },
                },
                OriginServers = new List<string>
                {
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

        private static async Task Server4Route(HttpContextBase ctx)
        {
            Console.WriteLine("| Server 4");
            Console.WriteLine("| Received URL: " + ctx.Request.Url.Full);
            if (!String.IsNullOrEmpty(ctx.Request.Query.Querystring))
                Console.WriteLine("| Querystring: " + ctx.Request.Query.Querystring);

            ctx.Response.StatusCode = 200;
            ctx.Response.ServerSentEvents = true;

            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(500);
                await ctx.Response.SendEvent("Event " + i.ToString(), false);
            }

            await ctx.Response.SendEvent(null, true);
            return;
        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}