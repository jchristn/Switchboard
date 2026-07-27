namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using RestWrapper;
    using Switchboard.Core;
    using Test.Shared.Harness;
    using Touchstone.Core;

    using HttpMethod = System.Net.Http.HttpMethod;

    /// <summary>
    /// Integration suites that exercise the Switchboard proxy pipeline end-to-end against the
    /// shared always-healthy harness: routing, authentication, REST verbs, header/query forwarding,
    /// URL rewriting, CORS, OpenAPI/Swagger, and load balancing.
    /// </summary>
    public static class ProxySuites
    {
        /// <summary>
        /// All proxy integration suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    ProxyCoreSuite(),
                    OpenApiSuite(),
                    LoadBalancingSuite()
                };
            }
        }

        /// <summary>
        /// Core proxy behavior: routing, auth, REST verbs, forwarding, rewriting, CORS.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ProxyCoreSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ProxyCore",
                displayName: "Proxy Core",
                beforeSuiteAsync: IntegrationSupport.EnsureSharedStarted,
                cases: new List<TestCaseDescriptor>
                {
                    IntegrationSupport.SharedCase("ProxyCore", "UnauthenticatedSuccess", "Unauthenticated GETs succeed", async (h, ct) =>
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            string url = h.Url("/unauthenticated" + (i % 2 == 0 ? "?foo=bar" : ""));
                            using (RestRequest req = new RestRequest(url))
                            using (RestResponse resp = await req.SendAsync())
                                Check.Equal(200, resp.StatusCode, "unauthenticated request " + i);
                        }
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "InvalidUrlNoMatch", "Unmatched URL returns non-200", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/undefined")))
                        using (RestResponse resp = await req.SendAsync())
                            Check.Equal(400, resp.StatusCode, "unmatched URL status");
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "AuthenticationRequired", "Authenticated route requires Authorization header", async (h, ct) =>
                    {
                        using (RestRequest denied = new RestRequest(h.Url("/authenticated")))
                        using (RestResponse resp = await denied.SendAsync())
                            Check.Equal(401, resp.StatusCode, "missing auth is 401");

                        using (RestRequest allowed = new RestRequest(h.Url("/authenticated")))
                        {
                            allowed.Authorization.BearerToken = "foo";
                            using (RestResponse resp = await allowed.SendAsync())
                                Check.Equal(200, resp.StatusCode, "with auth is 200");
                        }
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "UnmatchedMethods", "PUT/DELETE/PATCH on GET-only route are unmatched", async (h, ct) =>
                    {
                        foreach (string method in new[] { "PUT", "DELETE", "PATCH" })
                        {
                            using (RestRequest req = new RestRequest(h.Url("/unauthenticated"), new HttpMethod(method)))
                            using (RestResponse resp = await req.SendAsync())
                                Check.False(resp.StatusCode == 200, method + " should not match");
                        }
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "RestPost", "POST /api/users returns 201 and echoes payload", async (h, ct) =>
                    {
                        string payload = IntegrationSupport.Json.SerializeJson(new Dictionary<string, object> { { "name", "John Doe" }, { "email", "john.doe@example.com" }, { "age", 30 } }, false);
                        using (RestRequest req = new RestRequest(h.Url("/api/users"), HttpMethod.Post))
                        {
                            req.ContentType = "application/json";
                            using (RestResponse resp = await req.SendAsync(payload))
                            {
                                Check.Equal(201, resp.StatusCode, "POST status");
                                Check.Contains(resp.DataAsString, "john.doe@example.com", "POST echo");
                            }
                        }
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "RestPut", "PUT /api/users/{id} returns 200", async (h, ct) =>
                    {
                        string payload = IntegrationSupport.Json.SerializeJson(new Dictionary<string, object> { { "id", 123 }, { "email", "jane.smith@example.com" } }, false);
                        using (RestRequest req = new RestRequest(h.Url("/api/users/123"), HttpMethod.Put))
                        {
                            req.ContentType = "application/json";
                            using (RestResponse resp = await req.SendAsync(payload))
                            {
                                Check.Equal(200, resp.StatusCode, "PUT status");
                                Check.Contains(resp.DataAsString, "jane.smith@example.com", "PUT echo");
                            }
                        }
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "RestPatch", "PATCH /api/users/{id} returns 200", async (h, ct) =>
                    {
                        string payload = IntegrationSupport.Json.SerializeJson(new Dictionary<string, object> { { "age", 29 } }, false);
                        using (RestRequest req = new RestRequest(h.Url("/api/users/123"), HttpMethod.Patch))
                        {
                            req.ContentType = "application/json";
                            using (RestResponse resp = await req.SendAsync(payload))
                                Check.Equal(200, resp.StatusCode, "PATCH status");
                        }
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "RestDelete", "DELETE /api/users/{id} returns 204", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/api/users/123"), HttpMethod.Delete))
                        using (RestResponse resp = await req.SendAsync())
                            Check.Equal(204, resp.StatusCode, "DELETE status");
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "ComplexQuery", "GET with complex query parameters is forwarded", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/api/data?filter=active&sort=name&page=1&limit=10&fields=id,name,email")))
                        using (RestResponse resp = await req.SendAsync())
                        {
                            Check.Equal(200, resp.StatusCode, "query status");
                            Check.Contains(resp.DataAsString, "filter=active", "query forwarded");
                        }
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "CustomHeadersForwarded", "Custom headers are forwarded to origin", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/api/headers-test")))
                        {
                            req.Headers.Add("X-Custom-Header", "test-value-123");
                            req.Headers.Add("X-Test-Client", "switchboard-test-v2");
                            using (RestResponse resp = await req.SendAsync())
                            {
                                Check.Equal(200, resp.StatusCode, "headers status");
                                Check.Contains(resp.DataAsString, "X-Custom-Header", "custom header name echoed");
                                Check.Contains(resp.DataAsString, "test-value-123", "custom header value echoed");
                                Check.Contains(resp.DataAsString, "switchboard-test-v2", "test client echoed");
                            }
                        }
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "BlockedHeaderStripped", "Configured blocked headers are not forwarded to origins", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/api/headers-test")))
                        {
                            req.Headers.Add("X-Blocked-Secret", "should-not-arrive");
                            req.Headers.Add("X-Allowed-Header", "should-arrive");
                            using (RestResponse resp = await req.SendAsync())
                            {
                                Check.Equal(200, resp.StatusCode, "headers status");
                                Check.Contains(resp.DataAsString, "X-Allowed-Header", "allowed header forwarded");
                                Check.Contains(resp.DataAsString, "should-arrive", "allowed value forwarded");
                                Check.False(resp.DataAsString.Contains("should-not-arrive"), "blocked header value not forwarded");
                                Check.False(resp.DataAsString.Contains("X-Blocked-Secret"), "blocked header name not forwarded");
                            }
                        }
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "QueryForwarding", "Query strings are forwarded verbatim", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/unauthenticated?param1=value1")))
                        using (RestResponse resp = await req.SendAsync())
                        {
                            Check.Equal(200, resp.StatusCode, "simple query status");
                            Check.Contains(resp.DataAsString, "param1=value1", "simple query echoed");
                        }

                        using (RestRequest req = new RestRequest(h.Url("/api/query-test?param1=value1&param2=value2")))
                        using (RestResponse resp = await req.SendAsync())
                        {
                            Check.Equal(200, resp.StatusCode, "multi query status");
                            Check.Contains(resp.DataAsString, "param1=value1", "multi query p1");
                            Check.Contains(resp.DataAsString, "param2=value2", "multi query p2");
                        }
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "UrlRewriteApplied", "URL is rewritten before forwarding to origin", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/api/v2/users/12345")))
                        using (RestResponse resp = await req.SendAsync())
                        {
                            Check.Equal(200, resp.StatusCode, "rewrite status");
                            Check.Contains(resp.DataAsString, "/v1/users/12345", "origin saw rewritten path");
                        }
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "CorsPreflight", "OPTIONS preflight returns allow-methods", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/api/cors-test"), HttpMethod.Options))
                        {
                            req.Headers.Add("Access-Control-Request-Method", "POST");
                            req.Headers.Add("Access-Control-Request-Headers", "Content-Type");
                            req.Headers.Add("Origin", "http://example.com");
                            using (RestResponse resp = await req.SendAsync())
                            {
                                Check.Equal(200, resp.StatusCode, "preflight status");
                                string allow = resp.Headers.Get("Access-Control-Allow-Methods") ?? "";
                                Check.Contains(allow, "POST", "allow methods");
                            }
                        }
                    }),

                    IntegrationSupport.SharedCase("ProxyCore", "AuthHeaderForwarded", "Authorization header is forwarded to origin", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/api/secure")))
                        {
                            req.Authorization.BearerToken = "test-token-12345";
                            using (RestResponse resp = await req.SendAsync())
                            {
                                Check.Equal(200, resp.StatusCode, "secure status");
                                Check.Contains(resp.DataAsString, "Authorization", "auth header echoed");
                                Check.Contains(resp.DataAsString, "Bearer test-token-12345", "auth token echoed");
                            }
                        }
                    })
                });
        }

        /// <summary>
        /// OpenAPI document and Swagger UI behavior.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor OpenApiSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "OpenApi",
                displayName: "OpenAPI and Swagger",
                beforeSuiteAsync: IntegrationSupport.EnsureSharedStarted,
                cases: new List<TestCaseDescriptor>
                {
                    IntegrationSupport.SharedCase("OpenApi", "Document", "OpenAPI JSON document is served", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/openapi.json")))
                        using (RestResponse resp = await req.SendAsync())
                        {
                            Check.Equal(200, resp.StatusCode, "openapi status");
                            Check.Contains(resp.ContentType ?? "", "application/json", "openapi content type");
                            string json = resp.DataAsString;
                            Check.Contains(json, "\"openapi\"", "openapi field");
                            Check.Contains(json, "3.0.3", "openapi version");
                            Check.Contains(json, "\"info\"", "info section");
                            Check.Contains(json, "\"paths\"", "paths section");
                            Check.Contains(json, "Switchboard Test API", "api title");
                        }
                    }),

                    IntegrationSupport.SharedCase("OpenApi", "Paths", "OpenAPI document contains expected paths and methods", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/openapi.json")))
                        using (RestResponse resp = await req.SendAsync())
                        {
                            string json = resp.DataAsString;
                            foreach (string path in new[] { "/unauthenticated", "/api/users", "/events", "/authenticated", "/openapi.json", "/swagger" })
                                Check.Contains(json, path, "path " + path);
                            Check.Contains(json, "\"get\"", "get method");
                            Check.Contains(json, "\"post\"", "post method");
                        }
                    }),

                    IntegrationSupport.SharedCase("OpenApi", "SecuritySchemes", "OpenAPI document contains security schemes", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/openapi.json")))
                        using (RestResponse resp = await req.SendAsync())
                        {
                            string json = resp.DataAsString;
                            Check.Contains(json, "\"securitySchemes\"", "security schemes");
                            Check.Contains(json, "bearerAuth", "bearer auth scheme");
                            Check.Contains(json, "\"security\"", "security requirements");
                        }
                    }),

                    IntegrationSupport.SharedCase("OpenApi", "CustomDocs", "OpenAPI document contains custom route documentation", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/openapi.json")))
                        using (RestResponse resp = await req.SendAsync())
                        {
                            string json = resp.DataAsString;
                            Check.Contains(json, "getUsers", "getUsers operation");
                            Check.Contains(json, "List all users", "getUsers summary");
                            Check.Contains(json, "createUser", "createUser operation");
                            Check.Contains(json, "getEvents", "getEvents operation");
                            Check.Contains(json, "\"Users\"", "Users tag");
                            Check.Contains(json, "\"Events\"", "Events tag");
                        }
                    }),

                    IntegrationSupport.SharedCase("OpenApi", "PathParameters", "OpenAPI document extracts path parameters", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/openapi.json")))
                        using (RestResponse resp = await req.SendAsync())
                        {
                            string json = resp.DataAsString;
                            Check.Contains(json, "\"parameters\"", "parameters section");
                            Check.True(json.Contains("\"in\":\"path\"") || json.Contains("\"in\": \"path\""), "path parameter present");
                        }
                    }),

                    IntegrationSupport.SharedCase("OpenApi", "SwaggerUi", "Swagger UI HTML page is served", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/swagger")))
                        using (RestResponse resp = await req.SendAsync())
                        {
                            Check.Equal(200, resp.StatusCode, "swagger status");
                            Check.Contains(resp.ContentType ?? "", "text/html", "swagger content type");
                            string html = resp.DataAsString;
                            Check.Contains(html, "swagger-ui", "swagger ui div");
                            Check.Contains(html, "SwaggerUIBundle", "swagger bundle");
                            Check.Contains(html, "/openapi.json", "openapi reference");
                        }
                    }),

                    IntegrationSupport.SharedCase("OpenApi", "DocumentationPreflight", "OPTIONS preflight works for documentation routes", async (h, ct) =>
                    {
                        foreach (string path in new[] { "/openapi.json", "/swagger" })
                        {
                            using (RestRequest req = new RestRequest(h.Url(path), HttpMethod.Options))
                            {
                                req.Headers.Add("Access-Control-Request-Method", "GET");
                                req.Headers.Add("Access-Control-Request-Headers", "Content-Type");
                                req.Headers.Add("Origin", "http://example.com");
                                using (RestResponse resp = await req.SendAsync())
                                {
                                    Check.Equal(200, resp.StatusCode, "preflight status for " + path);
                                    string allow = resp.Headers.Get("Access-Control-Allow-Methods") ?? "";
                                    Check.Contains(allow, "GET", "allow GET for " + path);
                                }
                            }
                        }
                    })
                });
        }

        /// <summary>
        /// Load-balancing distribution across origins.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor LoadBalancingSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "LoadBalancing",
                displayName: "Load Balancing",
                beforeSuiteAsync: IntegrationSupport.EnsureSharedStarted,
                cases: new List<TestCaseDescriptor>
                {
                    IntegrationSupport.SharedCase("LoadBalancing", "RoundRobin", "Round robin spreads requests across all origins", async (h, ct) =>
                    {
                        HashSet<string> servers = await CollectServersAsync(h, 12);
                        Check.True(servers.Count >= 4, "all four origins received requests (saw " + servers.Count + ")");
                    }),

                    IntegrationSupport.SharedCase("LoadBalancing", "Random", "Random load balancing spreads requests across origins", async (h, ct) =>
                    {
                        LoadBalancingMode original = h.Settings.Endpoints[0].LoadBalancing;
                        h.Settings.Endpoints[0].LoadBalancing = LoadBalancingMode.Random;
                        try
                        {
                            HashSet<string> servers = await CollectServersAsync(h, 24);
                            Check.True(servers.Count >= 4, "all four origins received requests (saw " + servers.Count + ")");
                        }
                        finally
                        {
                            h.Settings.Endpoints[0].LoadBalancing = original;
                        }
                    })
                });
        }

        private static async Task<HashSet<string>> CollectServersAsync(ProxyHarness h, int requests)
        {
            HashSet<string> servers = new HashSet<string>();
            for (int i = 0; i < requests; i++)
            {
                using (RestRequest req = new RestRequest(h.Url("/unauthenticated")))
                using (RestResponse resp = await req.SendAsync())
                {
                    if (resp.StatusCode == 200)
                    {
                        string body = resp.DataAsString ?? "";
                        foreach (string name in new[] { "Server 1", "Server 2", "Server 3", "Server 4" })
                        {
                            if (body.Contains(name)) servers.Add(name);
                        }
                    }
                }
                await Task.Delay(50).ConfigureAwait(false);
            }
            return servers;
        }
    }
}
