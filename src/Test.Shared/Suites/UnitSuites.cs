namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Switchboard.Core;
    using Switchboard.Core.Database;
    using Switchboard.Core.Models;
    using Switchboard.Core.Settings;
    using Touchstone.Core;

    using SerializationHelper;

    /// <summary>
    /// Network-free unit suites covering URL rewriting, configuration models, settings
    /// (de)serialization, error responses, auth context, and database configuration.
    /// </summary>
    public static class UnitSuites
    {
        /// <summary>
        /// All unit suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    UrlRewriteSuite(),
                    ModelValidationSuite(),
                    SettingsSerializationSuite(),
                    ErrorResponseSuite(),
                    AuthContextSuite(),
                    DatabaseAndManagementSuite(),
                    CredentialSuite()
                };
            }
        }

        private static ApiEndpoint RewriteEndpoint()
        {
            ApiEndpoint endpoint = new ApiEndpoint();
            endpoint.Identifier = "testendpoint";
            endpoint.Name = "Test Endpoint";
            endpoint.LoadBalancing = LoadBalancingMode.RoundRobin;
            endpoint.OriginServers = new List<string> { "server1", "server2", "server3" };
            endpoint.RewriteUrls = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "GET", new Dictionary<string, string>
                    {
                        { "/v1.0/users/{UserGuid}", "/rewritten-users/{UserGuid}" },
                        { "/v1.0/people/{UnusedGuid}", "/rewritten-people/unused" },
                        { "/api/v2/users/{userId}", "/v1/users/{userId}" }
                    }
                }
            };
            return endpoint;
        }

        /// <summary>
        /// URL rewriting semantics of <see cref="UrlTools.RewriteUrl"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor UrlRewriteSuite()
        {
            ApiEndpoint endpoint = RewriteEndpoint();

            return new TestSuiteDescriptor(
                suiteId: "UrlRewrite",
                displayName: "URL Rewriting",
                cases: new List<TestCaseDescriptor>
                {
                    Case("UrlRewrite", "PassthroughNoRule", "No matching rule passes URL through unchanged", ct =>
                    {
                        Check.Equal("/foo", UrlTools.RewriteUrl("GET", "/foo", endpoint), "no-rule passthrough");
                        Check.Equal("/bar", UrlTools.RewriteUrl("GET", "/bar", endpoint), "no-rule passthrough");
                        return Task.CompletedTask;
                    }),

                    Case("UrlRewrite", "NoParamSegmentPassthrough", "Partial pattern without parameter segment passes through", ct =>
                    {
                        Check.Equal("/v1.0/users", UrlTools.RewriteUrl("GET", "/v1.0/users", endpoint), "partial passthrough");
                        Check.Equal("/v1.0/people", UrlTools.RewriteUrl("GET", "/v1.0/people", endpoint), "partial passthrough");
                        return Task.CompletedTask;
                    }),

                    Case("UrlRewrite", "SubstitutesParameter", "Parameter is captured and substituted into target", ct =>
                    {
                        Check.Equal("/rewritten-users/foo", UrlTools.RewriteUrl("GET", "/v1.0/users/foo", endpoint), "param substitution");
                        return Task.CompletedTask;
                    }),

                    Case("UrlRewrite", "DropsUnusedParameter", "Captured parameter not referenced by target is dropped", ct =>
                    {
                        Check.Equal("/rewritten-people/unused", UrlTools.RewriteUrl("GET", "/v1.0/people/helloworld", endpoint), "param drop");
                        return Task.CompletedTask;
                    }),

                    Case("UrlRewrite", "ApiV2UsersRewrite", "/api/v2/users/{userId} rewrites to /v1/users/{userId}", ct =>
                    {
                        Check.Equal("/v1/users/12345", UrlTools.RewriteUrl("GET", "/api/v2/users/12345", endpoint), "api v2 rewrite");
                        return Task.CompletedTask;
                    }),

                    Case("UrlRewrite", "MethodIsCaseSensitive", "Method match is case-sensitive; lowercase does not rewrite", ct =>
                    {
                        Check.Equal("/v1.0/users/foo", UrlTools.RewriteUrl("get", "/v1.0/users/foo", endpoint), "case-sensitive method");
                        return Task.CompletedTask;
                    }),

                    Case("UrlRewrite", "UnknownMethodPassthrough", "Method with no rewrite table passes through", ct =>
                    {
                        Check.Equal("/v1.0/users/foo", UrlTools.RewriteUrl("POST", "/v1.0/users/foo", endpoint), "unknown method passthrough");
                        return Task.CompletedTask;
                    }),

                    Case("UrlRewrite", "NullAndEmptyInputs", "Null/empty url or endpoint returns the input url", ct =>
                    {
                        Check.Equal("/foo", UrlTools.RewriteUrl("GET", "/foo", null!), "null endpoint");
                        Check.True(string.IsNullOrEmpty(UrlTools.RewriteUrl("GET", "", endpoint)), "empty url");
                        Check.True(UrlTools.RewriteUrl("GET", null!, endpoint) == null, "null url");
                        return Task.CompletedTask;
                    }),

                    Case("UrlRewrite", "MultiplePlaceholders", "Multiple placeholders are reordered per target", ct =>
                    {
                        ApiEndpoint multi = new ApiEndpoint();
                        multi.OriginServers = new List<string> { "s1" };
                        multi.RewriteUrls = new Dictionary<string, Dictionary<string, string>>
                        {
                            { "GET", new Dictionary<string, string> { { "/{ver}/x/{id}", "/{id}/{ver}" } } }
                        };
                        Check.Equal("/99/v2", UrlTools.RewriteUrl("GET", "/v2/x/99", multi), "multi placeholder");
                        return Task.CompletedTask;
                    }),

                    Case("UrlRewrite", "EmptyRewriteTablePassthrough", "Endpoint with no rewrite rules passes through", ct =>
                    {
                        ApiEndpoint bare = new ApiEndpoint();
                        Check.Equal("/anything", UrlTools.RewriteUrl("GET", "/anything", bare), "empty table passthrough");
                        return Task.CompletedTask;
                    }),

                    Case("UrlRewrite", "AnyMethodRewriteApplies", "Empty-method (any) rewrite applies regardless of request method", ct =>
                    {
                        ApiEndpoint anyEp = new ApiEndpoint();
                        anyEp.OriginServers = new List<string> { "s1" };
                        anyEp.RewriteUrls = new Dictionary<string, Dictionary<string, string>>
                        {
                            { "", new Dictionary<string, string> { { "/foo", "/bar" }, { "/users/{id}", "/v1/users/{id}" } } }
                        };
                        Check.Equal("/bar", UrlTools.RewriteUrl("GET", "/foo", anyEp), "any-method GET");
                        Check.Equal("/bar", UrlTools.RewriteUrl("POST", "/foo", anyEp), "any-method POST");
                        Check.Equal("/v1/users/42", UrlTools.RewriteUrl("DELETE", "/users/42", anyEp), "any-method param substitution");
                        return Task.CompletedTask;
                    }),

                    Case("UrlRewrite", "MethodSpecificBeatsAnyMethod", "Method-specific rewrite takes precedence over any-method rewrite", ct =>
                    {
                        ApiEndpoint ep = new ApiEndpoint();
                        ep.OriginServers = new List<string> { "s1" };
                        ep.RewriteUrls = new Dictionary<string, Dictionary<string, string>>
                        {
                            { "GET", new Dictionary<string, string> { { "/x", "/get-x" } } },
                            { "", new Dictionary<string, string> { { "/x", "/any-x" } } }
                        };
                        Check.Equal("/get-x", UrlTools.RewriteUrl("GET", "/x", ep), "method-specific wins");
                        Check.Equal("/any-x", UrlTools.RewriteUrl("POST", "/x", ep), "falls back to any-method");
                        return Task.CompletedTask;
                    }),

                    Case("UrlRewrite", "EmptyHttpMethodAllowedOnModel", "UrlRewrite accepts an empty HTTP method (any)", ct =>
                    {
                        UrlRewrite rw = new UrlRewrite { EndpointIdentifier = "e", HttpMethod = "", SourcePattern = "/foo", TargetPattern = "/bar" };
                        Check.Equal("", rw.HttpMethod, "empty method stored as empty");
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// Configuration model defaults and validation for <see cref="ApiEndpoint"/>, <see cref="OriginServer"/>,
        /// <see cref="ApiEndpointGroup"/>, and <see cref="SwitchboardSettings"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ModelValidationSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Models",
                displayName: "Configuration Model Validation",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Models", "ApiEndpointDefaults", "ApiEndpoint exposes documented defaults", ct =>
                    {
                        ApiEndpoint e = new ApiEndpoint();
                        Check.Equal(60000, e.TimeoutMs, "TimeoutMs default");
                        Check.Equal(LoadBalancingMode.RoundRobin, e.LoadBalancing, "LoadBalancing default");
                        Check.Equal(536870912, e.MaxRequestBodySize, "MaxRequestBodySize default");
                        Check.True(e.IncludeAuthContextHeader, "IncludeAuthContextHeader default");
                        Check.Equal(Constants.AuthContextHeader, e.AuthContextHeader, "AuthContextHeader default");
                        Check.True(e.UseGlobalBlockedHeaders, "UseGlobalBlockedHeaders default");
                        return Task.CompletedTask;
                    }),

                    Case("Models", "ApiEndpointRangeValidation", "ApiEndpoint rejects out-of-range values", ct =>
                    {
                        Check.Throws<ArgumentOutOfRangeException>(() => new ApiEndpoint().TimeoutMs = -1, "negative TimeoutMs");
                        Check.Throws<ArgumentOutOfRangeException>(() => new ApiEndpoint().MaxRequestBodySize = 0, "zero MaxRequestBodySize");
                        Check.Throws<ArgumentOutOfRangeException>(() => new ApiEndpoint().MaxCaptureRequestBodySize = -1, "negative MaxCaptureRequestBodySize");
                        return Task.CompletedTask;
                    }),

                    Case("Models", "ApiEndpointLastIndex", "LastIndex requires populated OriginServers and is bounded", ct =>
                    {
                        ApiEndpoint e = new ApiEndpoint();
                        Check.Throws<ArgumentOutOfRangeException>(() => e.LastIndex = 0, "LastIndex with empty origins");
                        e.OriginServers = new List<string> { "a", "b" };
                        e.LastIndex = 1;
                        Check.Equal(1, e.LastIndex, "LastIndex within bounds");
                        Check.Throws<ArgumentOutOfRangeException>(() => e.LastIndex = 2, "LastIndex beyond bounds");
                        return Task.CompletedTask;
                    }),

                    Case("Models", "ApiEndpointGroupNullCoalesce", "ApiEndpointGroup coalesces null ParameterizedUrls", ct =>
                    {
                        ApiEndpointGroup g = new ApiEndpointGroup();
                        Check.True(g.ParameterizedUrls != null, "default not null");
                        g.ParameterizedUrls = null!;
                        Check.True(g.ParameterizedUrls != null, "null coalesced to empty");
                        Check.Equal(0, g.ParameterizedUrls!.Count, "coalesced empty");
                        return Task.CompletedTask;
                    }),

                    Case("Models", "OriginServerDefaults", "OriginServer exposes documented defaults", ct =>
                    {
                        OriginServer o = new OriginServer();
                        Check.Equal("localhost", o.Hostname, "Hostname default");
                        Check.Equal(8000, o.Port, "Port default");
                        Check.Equal(5000, o.HealthCheckIntervalMs, "HealthCheckIntervalMs default");
                        Check.Equal(2, o.UnhealthyThreshold, "UnhealthyThreshold default");
                        Check.Equal(2, o.HealthyThreshold, "HealthyThreshold default");
                        Check.Equal(10, o.MaxParallelRequests, "MaxParallelRequests default");
                        Check.Equal(30, o.RateLimitRequestsThreshold, "RateLimitRequestsThreshold default");
                        Check.Equal("/", o.HealthCheckUrl, "HealthCheckUrl default");
                        Check.False(o.Healthy, "Healthy default false");
                        return Task.CompletedTask;
                    }),

                    Case("Models", "OriginServerValidation", "OriginServer rejects invalid values", ct =>
                    {
                        Check.Throws<ArgumentOutOfRangeException>(() => new OriginServer().Port = 70000, "port too high");
                        Check.Throws<ArgumentOutOfRangeException>(() => new OriginServer().Port = -1, "port negative");
                        Check.Throws<ArgumentOutOfRangeException>(() => new OriginServer().HealthCheckIntervalMs = 999, "interval below minimum");
                        Check.Throws<ArgumentOutOfRangeException>(() => new OriginServer().UnhealthyThreshold = 0, "threshold below minimum");
                        Check.Throws<ArgumentNullException>(() => new OriginServer().Hostname = null!, "null hostname");
                        return Task.CompletedTask;
                    }),

                    Case("Models", "OriginServerHealthCheckUrlCoercion", "Empty HealthCheckUrl coerces to '/'", ct =>
                    {
                        OriginServer o = new OriginServer();
                        o.HealthCheckUrl = "";
                        Check.Equal("/", o.HealthCheckUrl, "empty coerced to /");
                        return Task.CompletedTask;
                    }),

                    Case("Models", "OriginServerUrlPrefix", "UrlPrefix reflects scheme, host, and port", ct =>
                    {
                        OriginServer o = new OriginServer();
                        o.Hostname = "localhost";
                        o.Port = 8000;
                        o.Ssl = false;
                        Check.Equal("http://localhost:8000", o.UrlPrefix, "http prefix");
                        o.Ssl = true;
                        Check.Equal("https://localhost:8000", o.UrlPrefix, "https prefix");
                        return Task.CompletedTask;
                    }),

                    Case("Models", "SettingsDefaults", "SwitchboardSettings has default blocked headers and non-null Webserver", ct =>
                    {
                        SwitchboardSettings s = new SwitchboardSettings();
                        Check.True(s.Webserver != null, "Webserver not null");
                        Check.True(s.BlockedHeaders.Contains("host"), "default blocked headers include host");
                        Check.True(s.BlockedHeaders.Contains("connection"), "default blocked headers include connection");
                        Check.Throws<ArgumentNullException>(() => s.Webserver = null!, "null Webserver rejected");
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// Settings (de)serialization round-trips, exercising the same path used to load sb.json.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor SettingsSerializationSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Settings",
                displayName: "Settings Serialization",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Settings", "RoundTrip", "Settings serialize and deserialize preserving endpoints and origins", ct =>
                    {
                        Serializer serializer = new Serializer();
                        SwitchboardSettings original = new SwitchboardSettings();

                        ApiEndpoint endpoint = new ApiEndpoint();
                        endpoint.Identifier = "e1";
                        endpoint.Name = "Endpoint One";
                        endpoint.LoadBalancing = LoadBalancingMode.Random;
                        endpoint.TimeoutMs = 12345;
                        endpoint.OriginServers = new List<string> { "o1" };
                        original.Endpoints.Add(endpoint);

                        OriginServer origin = new OriginServer();
                        origin.Identifier = "o1";
                        origin.Name = "Origin One";
                        origin.Hostname = "example.com";
                        origin.Port = 4321;
                        origin.Ssl = true;
                        original.Origins.Add(origin);

                        string json = serializer.SerializeJson(original, true);
                        SwitchboardSettings restored = serializer.DeserializeJson<SwitchboardSettings>(json);

                        Check.Equal(1, restored.Endpoints.Count, "endpoint count");
                        Check.Equal("e1", restored.Endpoints[0].Identifier, "endpoint id");
                        Check.Equal(LoadBalancingMode.Random, restored.Endpoints[0].LoadBalancing, "endpoint lb");
                        Check.Equal(12345, restored.Endpoints[0].TimeoutMs, "endpoint timeout");
                        Check.Equal(1, restored.Origins.Count, "origin count");
                        Check.Equal("example.com", restored.Origins[0].Hostname, "origin host");
                        Check.Equal(4321, restored.Origins[0].Port, "origin port");
                        Check.True(restored.Origins[0].Ssl, "origin ssl");
                        return Task.CompletedTask;
                    }),

                    Case("Settings", "DefaultsRoundTrip", "A default settings object survives a serialization round-trip", ct =>
                    {
                        Serializer serializer = new Serializer();
                        SwitchboardSettings original = new SwitchboardSettings();
                        string json = serializer.SerializeJson(original, true);
                        SwitchboardSettings restored = serializer.DeserializeJson<SwitchboardSettings>(json);
                        Check.True(restored.Webserver != null, "webserver preserved");
                        Check.True(restored.BlockedHeaders.Count > 0, "blocked headers preserved");
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// <see cref="ApiErrorResponse"/> code-to-status/message mapping.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ErrorResponseSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ErrorResponse",
                displayName: "API Error Responses",
                cases: new List<TestCaseDescriptor>
                {
                    Case("ErrorResponse", "StatusCodeMapping", "Error codes map to documented status codes", ct =>
                    {
                        Check.Equal(400, new ApiErrorResponse(ApiErrorEnum.BadRequest).StatusCode, "BadRequest");
                        Check.Equal(401, new ApiErrorResponse(ApiErrorEnum.AuthenticationFailed).StatusCode, "AuthenticationFailed");
                        Check.Equal(401, new ApiErrorResponse(ApiErrorEnum.AuthorizationFailed).StatusCode, "AuthorizationFailed");
                        Check.Equal(404, new ApiErrorResponse(ApiErrorEnum.NotFound).StatusCode, "NotFound");
                        Check.Equal(409, new ApiErrorResponse(ApiErrorEnum.Conflict).StatusCode, "Conflict");
                        Check.Equal(413, new ApiErrorResponse(ApiErrorEnum.TooLarge).StatusCode, "TooLarge");
                        Check.Equal(429, new ApiErrorResponse(ApiErrorEnum.SlowDown).StatusCode, "SlowDown");
                        Check.Equal(502, new ApiErrorResponse(ApiErrorEnum.BadGateway).StatusCode, "BadGateway");
                        Check.Equal(505, new ApiErrorResponse(ApiErrorEnum.UnsupportedHttpVersion).StatusCode, "UnsupportedHttpVersion");
                        return Task.CompletedTask;
                    }),

                    Case("ErrorResponse", "MessagePopulated", "Well-known errors carry a human-readable message", ct =>
                    {
                        Check.True(!string.IsNullOrEmpty(new ApiErrorResponse(ApiErrorEnum.BadRequest).Message), "BadRequest message");
                        Check.True(!string.IsNullOrEmpty(new ApiErrorResponse(ApiErrorEnum.BadGateway).Message), "BadGateway message");
                        return Task.CompletedTask;
                    }),

                    Case("ErrorResponse", "SlowDownAndTokenExpiredMessages", "SlowDown and TokenExpired have specific, non-generic messages", ct =>
                    {
                        string slowDown = new ApiErrorResponse(ApiErrorEnum.SlowDown).Message;
                        Check.False(slowDown.Contains("unknown error code"), "SlowDown is not the generic message");
                        Check.Contains(slowDown, "rate", "SlowDown mentions rate");

                        string tokenExpired = new ApiErrorResponse(ApiErrorEnum.TokenExpired).Message;
                        Check.False(tokenExpired.Contains("unknown error code"), "TokenExpired is not the generic message");
                        Check.Contains(tokenExpired, "expired", "TokenExpired mentions expiry");
                        return Task.CompletedTask;
                    }),

                    Case("ErrorResponse", "DefaultError", "Default error code is AuthenticationFailed", ct =>
                    {
                        Check.Equal(ApiErrorEnum.AuthenticationFailed, new ApiErrorResponse().Error, "default error");
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// <see cref="AuthContext"/> defaults, validation, and base64 round-trip.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor AuthContextSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "AuthContext",
                displayName: "Auth Context",
                cases: new List<TestCaseDescriptor>
                {
                    Case("AuthContext", "Defaults", "New AuthContext defaults to Success/Success", ct =>
                    {
                        AuthContext a = new AuthContext();
                        Check.Equal(AuthenticationResultEnum.Success, a.Authentication.Result, "auth default");
                        Check.Equal(AuthorizationResultEnum.Success, a.Authorization.Result, "authz default");
                        return Task.CompletedTask;
                    }),

                    Case("AuthContext", "NullNestedRejected", "Null nested contexts are rejected", ct =>
                    {
                        Check.Throws<ArgumentNullException>(() => new AuthContext().Authentication = null!, "null authentication");
                        Check.Throws<ArgumentNullException>(() => new AuthContext().Authorization = null!, "null authorization");
                        return Task.CompletedTask;
                    }),

                    Case("AuthContext", "Base64RoundTrip", "Base64 round-trip preserves results", ct =>
                    {
                        AuthContext original = new AuthContext();
                        original.Authentication.Result = AuthenticationResultEnum.Denied;
                        original.Authorization.Result = AuthorizationResultEnum.Denied;
                        string encoded = original.ToBase64String();
                        AuthContext restored = AuthContext.FromBase64String(encoded);
                        Check.Equal(AuthenticationResultEnum.Denied, restored.Authentication.Result, "restored auth");
                        Check.Equal(AuthorizationResultEnum.Denied, restored.Authorization.Result, "restored authz");
                        return Task.CompletedTask;
                    }),

                    Case("AuthContext", "FromBase64NullThrows", "FromBase64String rejects null/empty", ct =>
                    {
                        Check.Throws<ArgumentNullException>(() => AuthContext.FromBase64String(null!), "null base64");
                        return Task.CompletedTask;
                    }),

                    Case("AuthContext", "TryFromBase64Invalid", "TryFromBase64String returns false on malformed input", ct =>
                    {
                        bool ok = AuthContext.TryFromBase64String("!!!not-base64!!!", out AuthContext parsed);
                        Check.False(ok, "malformed returns false");
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// <see cref="DatabaseSettings"/> and <see cref="ManagementSettings"/> behavior.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor DatabaseAndManagementSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "DbConfig",
                displayName: "Database and Management Settings",
                cases: new List<TestCaseDescriptor>
                {
                    Case("DbConfig", "SqliteDefaults", "DatabaseSettings default to Sqlite with sb.db", ct =>
                    {
                        DatabaseSettings d = new DatabaseSettings();
                        Check.Equal(DatabaseTypeEnum.Sqlite, d.Type, "default type");
                        Check.Equal("sb.db", d.Filename, "default filename");
                        Check.Equal("Data Source=sb.db", d.BuildConnectionString(), "sqlite conn string");
                        return Task.CompletedTask;
                    }),

                    Case("DbConfig", "PostgresConnectionString", "Postgres connection string is composed from parts", ct =>
                    {
                        DatabaseSettings d = new DatabaseSettings();
                        d.Type = DatabaseTypeEnum.Postgres;
                        d.Hostname = "dbhost";
                        d.DatabaseName = "switchboard";
                        d.Username = "user";
                        d.Password = "pass";
                        string conn = d.BuildConnectionString();
                        Check.Contains(conn, "Host=dbhost", "host");
                        Check.Contains(conn, "Database=switchboard", "database");
                        Check.Contains(conn, "Port=5432", "default port");
                        return Task.CompletedTask;
                    }),

                    Case("DbConfig", "ExplicitConnectionStringWins", "An explicit connection string takes precedence", ct =>
                    {
                        DatabaseSettings d = new DatabaseSettings();
                        d.Type = DatabaseTypeEnum.Postgres;
                        d.ConnectionString = "Host=override;Database=x";
                        Check.Equal("Host=override;Database=x", d.BuildConnectionString(), "explicit wins");
                        return Task.CompletedTask;
                    }),

                    Case("DbConfig", "ManagementBasePathNormalization", "ManagementSettings normalizes BasePath", ct =>
                    {
                        ManagementSettings m = new ManagementSettings();
                        Check.Equal("/_sb/v1.0/", m.BasePath, "default base path");
                        m.BasePath = "foo";
                        Check.Equal("/foo/", m.BasePath, "normalized base path");
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// <see cref="Credential"/> token generation and hashing.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor CredentialSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Credential",
                displayName: "Credential Tokens",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Credential", "GenerateBearerToken", "Generated tokens are URL-safe and non-empty", ct =>
                    {
                        string token = Credential.GenerateBearerToken();
                        Check.True(!string.IsNullOrEmpty(token), "non-empty");
                        Check.False(token.Contains("+"), "no plus");
                        Check.False(token.Contains("/"), "no slash");
                        Check.False(token.Contains("="), "no padding");
                        return Task.CompletedTask;
                    }),

                    Case("Credential", "ComputeTokenHashDeterministic", "Token hashing is deterministic and non-empty", ct =>
                    {
                        string hashA = Credential.ComputeTokenHash("hello-token");
                        string hashB = Credential.ComputeTokenHash("hello-token");
                        Check.True(!string.IsNullOrEmpty(hashA), "hash non-empty");
                        Check.Equal(hashA, hashB, "hash deterministic");
                        Check.Equal(string.Empty, Credential.ComputeTokenHash(""), "empty token empty hash");
                        return Task.CompletedTask;
                    })
                });
        }

        private static TestCaseDescriptor Case(
            string suiteId,
            string caseId,
            string displayName,
            Func<System.Threading.CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor(suiteId, caseId, displayName, executeAsync);
        }
    }
}
