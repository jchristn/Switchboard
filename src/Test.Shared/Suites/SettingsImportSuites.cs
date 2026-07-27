namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Switchboard.Core;
    using Switchboard.Core.Client;
    using Switchboard.Core.Database;
    using Switchboard.Core.Models;
    using Switchboard.Core.Services;
    using Switchboard.Core.Settings;
    using SyslogLogging;
    using Touchstone.Core;
    using WatsonWebserver.Core;

    /// <summary>
    /// Exercises <see cref="SettingsImportService"/> against fresh temporary SQLite databases.
    /// Each case is fully self-contained (its own database) so it can run in any order or runner.
    /// </summary>
    public static class SettingsImportSuites
    {
        private static readonly LoggingModule _Logging = CreateQuietLogging();

        private static LoggingModule CreateQuietLogging()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            logging.Settings.MinimumSeverity = Severity.Alert;
            return logging;
        }

        /// <summary>
        /// The settings-import suite.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the settings-import suite descriptor.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "SettingsImport",
                displayName: "Settings Import Service",
                cases: new List<TestCaseDescriptor>
                {
                    Case("ImportOrigins", "Initial import of origins to empty database", async ctx =>
                    {
                        ctx.Settings.Origins.Add(new OriginServer
                        {
                            Identifier = "origin1",
                            Name = "Origin Server 1",
                            Hostname = "localhost",
                            Port = 8001,
                            Ssl = false,
                            HealthCheckIntervalMs = 5000,
                            HealthCheckMethod = HttpMethod.GET,
                            HealthCheckUrl = "/health",
                            UnhealthyThreshold = 3,
                            HealthyThreshold = 2,
                            MaxParallelRequests = 10,
                            RateLimitRequestsThreshold = 100
                        });
                        ctx.Settings.Origins.Add(new OriginServer { Identifier = "origin2", Name = "Origin Server 2", Hostname = "localhost", Port = 8002, Ssl = true });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        Check.True(await svc.HasItemsToImportAsync(), "HasItemsToImportAsync true");
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(2, result.OriginsImported, "OriginsImported");
                        Check.Equal(0, result.OriginsSkipped, "OriginsSkipped");

                        List<OriginServerConfig> origins = await ctx.Client.OriginServers.GetAllAsync();
                        Check.Equal(2, origins.Count, "db origin count");
                        OriginServerConfig? o1 = await ctx.Client.OriginServers.GetByIdentifierAsync("origin1");
                        Check.True(o1 != null, "origin1 present");
                        Check.Equal(8001, o1!.Port, "origin1 port");
                        Check.Equal("/health", o1.HealthCheckUrl, "origin1 health url");
                    }),

                    Case("ImportEndpoints", "Initial import of endpoints to empty database", async ctx =>
                    {
                        ctx.Settings.Origins.Add(new OriginServer { Identifier = "backend", Hostname = "localhost", Port = 9000 });
                        ctx.Settings.Endpoints.Add(new ApiEndpoint
                        {
                            Identifier = "api-v1",
                            Name = "API v1",
                            TimeoutMs = 30000,
                            LoadBalancing = LoadBalancingMode.RoundRobin,
                            BlockHttp10 = true,
                            MaxRequestBodySize = 1048576,
                            IncludeAuthContextHeader = true,
                            AuthContextHeader = "x-auth-context",
                            UseGlobalBlockedHeaders = true,
                            OriginServers = new List<string> { "backend" }
                        });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(1, result.EndpointsImported, "EndpointsImported");
                        Check.Equal(1, result.OriginsImported, "OriginsImported");

                        ApiEndpointConfig? cfg = await ctx.Client.ApiEndpoints.GetByIdentifierAsync("api-v1");
                        Check.True(cfg != null, "endpoint present");
                        Check.Equal(30000, cfg!.TimeoutMs, "endpoint timeout");
                        Check.Equal("RoundRobin", cfg.LoadBalancingMode, "endpoint lb mode");
                        Check.True(cfg.BlockHttp10, "endpoint block http10");
                        Check.Equal(1048576, cfg.MaxRequestBodySize, "endpoint max body");
                    }),

                    Case("SkipExistingOrigins", "Existing origins are not overwritten", async ctx =>
                    {
                        await ctx.Client.OriginServers.CreateAsync(new OriginServerConfig { Identifier = "existing-origin", Name = "Existing Origin (DB)", Hostname = "db-host", Port = 1234 });
                        ctx.Settings.Origins.Add(new OriginServer { Identifier = "existing-origin", Name = "Settings Origin", Hostname = "settings-host", Port = 9999 });
                        ctx.Settings.Origins.Add(new OriginServer { Identifier = "new-origin", Hostname = "localhost", Port = 8080 });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(1, result.OriginsImported, "OriginsImported");
                        Check.Equal(1, result.OriginsSkipped, "OriginsSkipped");

                        OriginServerConfig? existing = await ctx.Client.OriginServers.GetByIdentifierAsync("existing-origin");
                        Check.Equal("Existing Origin (DB)", existing!.Name, "existing name preserved");
                        Check.Equal("db-host", existing.Hostname, "existing host preserved");
                        Check.True(await ctx.Client.OriginServers.GetByIdentifierAsync("new-origin") != null, "new origin present");
                    }),

                    Case("SkipExistingEndpoints", "Existing endpoints are not overwritten", async ctx =>
                    {
                        await ctx.Client.ApiEndpoints.CreateAsync(new ApiEndpointConfig { Identifier = "existing-endpoint", TimeoutMs = 1000 });
                        ctx.Settings.Endpoints.Add(new ApiEndpoint { Identifier = "existing-endpoint", TimeoutMs = 9999 });
                        ctx.Settings.Endpoints.Add(new ApiEndpoint { Identifier = "new-endpoint" });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(1, result.EndpointsImported, "EndpointsImported");
                        Check.Equal(1, result.EndpointsSkipped, "EndpointsSkipped");

                        ApiEndpointConfig? existing = await ctx.Client.ApiEndpoints.GetByIdentifierAsync("existing-endpoint");
                        Check.Equal(1000, existing!.TimeoutMs, "existing timeout preserved");
                    }),

                    Case("UnauthenticatedRoutes", "Unauthenticated routes import with auth flag false", async ctx =>
                    {
                        ctx.Settings.Endpoints.Add(new ApiEndpoint
                        {
                            Identifier = "public-api",
                            Unauthenticated = new ApiEndpointGroup
                            {
                                ParameterizedUrls = new Dictionary<string, List<string>>
                                {
                                    { "GET", new List<string> { "/health", "/status", "/api/users/{id}" } },
                                    { "POST", new List<string> { "/api/signup" } }
                                }
                            }
                        });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(4, result.RoutesImported, "RoutesImported");

                        List<EndpointRoute> routes = (await ctx.Client.EndpointRoutes.GetAllAsync()).Where(r => r.EndpointIdentifier == "public-api").ToList();
                        Check.Equal(4, routes.Count, "db route count");
                        foreach (EndpointRoute r in routes)
                            Check.False(r.RequiresAuthentication, "route " + r.HttpMethod + " " + r.UrlPattern + " unauthenticated");
                        Check.True(routes.Any(r => r.HttpMethod == "GET" && r.UrlPattern == "/health"), "GET /health present");
                        Check.True(routes.Any(r => r.HttpMethod == "POST" && r.UrlPattern == "/api/signup"), "POST /api/signup present");
                    }),

                    Case("AuthenticatedRoutes", "Authenticated routes import with auth flag true", async ctx =>
                    {
                        ctx.Settings.Endpoints.Add(new ApiEndpoint
                        {
                            Identifier = "secure-api",
                            Authenticated = new ApiEndpointGroup
                            {
                                ParameterizedUrls = new Dictionary<string, List<string>>
                                {
                                    { "GET", new List<string> { "/api/profile", "/api/settings" } },
                                    { "PUT", new List<string> { "/api/profile" } },
                                    { "DELETE", new List<string> { "/api/account" } }
                                }
                            }
                        });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(4, result.RoutesImported, "RoutesImported");

                        List<EndpointRoute> routes = (await ctx.Client.EndpointRoutes.GetAllAsync()).Where(r => r.EndpointIdentifier == "secure-api").ToList();
                        Check.Equal(4, routes.Count, "db route count");
                        foreach (EndpointRoute r in routes)
                            Check.True(r.RequiresAuthentication, "route authenticated");
                    }),

                    Case("MixedRoutes", "Mixed authenticated and unauthenticated routes", async ctx =>
                    {
                        ctx.Settings.Endpoints.Add(new ApiEndpoint
                        {
                            Identifier = "mixed-api",
                            Unauthenticated = new ApiEndpointGroup { ParameterizedUrls = new Dictionary<string, List<string>> { { "GET", new List<string> { "/public" } } } },
                            Authenticated = new ApiEndpointGroup { ParameterizedUrls = new Dictionary<string, List<string>> { { "GET", new List<string> { "/private" } } } }
                        });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        await svc.ImportAsync();

                        List<EndpointRoute> routes = (await ctx.Client.EndpointRoutes.GetAllAsync()).Where(r => r.EndpointIdentifier == "mixed-api").ToList();
                        EndpointRoute? pub = routes.Find(r => r.UrlPattern == "/public");
                        EndpointRoute? priv = routes.Find(r => r.UrlPattern == "/private");
                        Check.True(pub != null, "public route present");
                        Check.True(priv != null, "private route present");
                        Check.False(pub!.RequiresAuthentication, "public unauthenticated");
                        Check.True(priv!.RequiresAuthentication, "private authenticated");
                    }),

                    Case("OriginMappings", "Endpoint maps to multiple origins in order", async ctx =>
                    {
                        ctx.Settings.Origins.Add(new OriginServer { Identifier = "origin-a", Hostname = "localhost", Port = 8001 });
                        ctx.Settings.Origins.Add(new OriginServer { Identifier = "origin-b", Hostname = "localhost", Port = 8002 });
                        ctx.Settings.Origins.Add(new OriginServer { Identifier = "origin-c", Hostname = "localhost", Port = 8003 });
                        ctx.Settings.Endpoints.Add(new ApiEndpoint { Identifier = "lb-endpoint", OriginServers = new List<string> { "origin-a", "origin-b", "origin-c" } });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(3, result.MappingsImported, "MappingsImported");

                        List<EndpointOriginMapping> mappings = (await ctx.Client.EndpointOriginMappings.GetAllAsync()).Where(m => m.EndpointIdentifier == "lb-endpoint").ToList();
                        Check.Equal(3, mappings.Count, "mapping count");
                    }),

                    Case("MissingOriginReference", "Missing origin reference is skipped in mappings", async ctx =>
                    {
                        ctx.Settings.Origins.Add(new OriginServer { Identifier = "exists", Hostname = "localhost", Port = 8001 });
                        ctx.Settings.Endpoints.Add(new ApiEndpoint { Identifier = "endpoint-x", OriginServers = new List<string> { "exists", "does-not-exist" } });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(1, result.EndpointsImported, "EndpointsImported");
                        Check.Equal(1, result.MappingsImported, "MappingsImported");
                    }),

                    Case("UrlRewrites", "URL rewrite rules import", async ctx =>
                    {
                        ctx.Settings.Endpoints.Add(new ApiEndpoint
                        {
                            Identifier = "rewrite-endpoint",
                            RewriteUrls = new Dictionary<string, Dictionary<string, string>>
                            {
                                { "GET", new Dictionary<string, string> { { "/api/v1/users/{id}", "/users/{id}" }, { "/api/v1/products/{id}", "/products/{id}" } } },
                                { "POST", new Dictionary<string, string> { { "/api/v1/users", "/users" } } }
                            }
                        });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(3, result.RewritesImported, "RewritesImported");

                        List<UrlRewrite> rewrites = (await ctx.Client.UrlRewrites.GetAllAsync()).Where(r => r.EndpointIdentifier == "rewrite-endpoint").ToList();
                        Check.Equal(3, rewrites.Count, "rewrite count");
                        UrlRewrite? users = rewrites.Find(r => r.HttpMethod == "GET" && r.SourcePattern == "/api/v1/users/{id}");
                        Check.True(users != null, "users rewrite present");
                        Check.Equal("/users/{id}", users!.TargetPattern, "rewrite target");
                    }),

                    Case("BlockedHeaders", "Global blocked headers import", async ctx =>
                    {
                        ctx.Settings.BlockedHeaders = new List<string> { "x-custom-header", "x-internal-token", "x-debug-info" };

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(3, result.BlockedHeadersImported, "BlockedHeadersImported");
                        Check.True(await ctx.Client.BlockedHeaders.IsBlockedAsync("x-custom-header"), "header blocked");
                    }),

                    Case("BlockedHeadersSkip", "Existing blocked headers are skipped", async ctx =>
                    {
                        await ctx.Client.BlockedHeaders.CreateAsync(new BlockedHeader("existing-header"));
                        ctx.Settings.BlockedHeaders = new List<string> { "existing-header", "new-header" };

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(1, result.BlockedHeadersImported, "BlockedHeadersImported");
                        List<BlockedHeader> all = await ctx.Client.BlockedHeaders.GetAllAsync();
                        Check.Equal(2, all.Count, "total blocked headers");
                    }),

                    Case("NullOriginIdentifier", "Origin with null identifier is skipped", async ctx =>
                    {
                        ctx.Settings.Origins.Add(new OriginServer { Identifier = null, Hostname = "localhost", Port = 8001 });
                        ctx.Settings.Origins.Add(new OriginServer { Identifier = "valid-origin", Hostname = "localhost", Port = 8002 });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(1, result.OriginsImported, "OriginsImported");
                        List<OriginServerConfig> origins = await ctx.Client.OriginServers.GetAllAsync();
                        Check.Equal(1, origins.Count, "db origin count");
                    }),

                    Case("NullEndpointIdentifier", "Endpoint with null identifier is skipped", async ctx =>
                    {
                        ctx.Settings.Endpoints.Add(new ApiEndpoint { Identifier = null });
                        ctx.Settings.Endpoints.Add(new ApiEndpoint { Identifier = "valid-endpoint" });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(1, result.EndpointsImported, "EndpointsImported");
                        List<ApiEndpointConfig> endpoints = await ctx.Client.ApiEndpoints.GetAllAsync();
                        Check.Equal(1, endpoints.Count, "db endpoint count");
                    }),

                    Case("HasItemsEmpty", "HasItemsToImportAsync false for empty settings", async ctx =>
                    {
                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        Check.False(await svc.HasItemsToImportAsync(), "empty returns false");
                    }),

                    Case("HasItemsEndpoints", "HasItemsToImportAsync true with an endpoint", async ctx =>
                    {
                        ctx.Settings.Endpoints.Add(new ApiEndpoint { Identifier = "test" });
                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        Check.True(await svc.HasItemsToImportAsync(), "endpoint returns true");
                    }),

                    Case("HasItemsOrigins", "HasItemsToImportAsync true with an origin", async ctx =>
                    {
                        ctx.Settings.Origins.Add(new OriginServer { Identifier = "test", Hostname = "localhost", Port = 8000 });
                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        Check.True(await svc.HasItemsToImportAsync(), "origin returns true");
                    }),

                    Case("EmptyRoutes", "Endpoint with no routes imports without error", async ctx =>
                    {
                        ctx.Settings.Endpoints.Add(new ApiEndpoint { Identifier = "empty-routes", Unauthenticated = new ApiEndpointGroup() });
                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult result = await svc.ImportAsync();
                        Check.Equal(1, result.EndpointsImported, "EndpointsImported");
                        Check.Equal(0, result.RoutesImported, "RoutesImported");
                    }),

                    Case("AllOriginProperties", "All origin properties round-trip through import", async ctx =>
                    {
                        ctx.Settings.Origins.Add(new OriginServer
                        {
                            Identifier = "full-origin",
                            Name = "Full Origin",
                            Hostname = "example.com",
                            Port = 443,
                            Ssl = true,
                            HealthCheckIntervalMs = 10000,
                            HealthCheckMethod = HttpMethod.HEAD,
                            HealthCheckUrl = "/healthz",
                            UnhealthyThreshold = 5,
                            HealthyThreshold = 3,
                            MaxParallelRequests = 50,
                            RateLimitRequestsThreshold = 200,
                            LogRequestBody = true,
                            LogResponseBody = true,
                            CaptureRequestBody = true,
                            CaptureResponseBody = true,
                            CaptureRequestHeaders = false,
                            CaptureResponseHeaders = false,
                            MaxCaptureRequestBodySize = 1024,
                            MaxCaptureResponseBodySize = 2048
                        });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        await svc.ImportAsync();

                        OriginServerConfig? cfg = await ctx.Client.OriginServers.GetByIdentifierAsync("full-origin");
                        Check.True(cfg != null, "origin present");
                        Check.Equal("example.com", cfg!.Hostname, "hostname");
                        Check.Equal(443, cfg.Port, "port");
                        Check.True(cfg.Ssl, "ssl");
                        Check.Equal(10000, cfg.HealthCheckIntervalMs, "interval");
                        Check.Equal("HEAD", cfg.HealthCheckMethod, "health method string");
                        Check.Equal("/healthz", cfg.HealthCheckUrl, "health url");
                        Check.Equal(5, cfg.UnhealthyThreshold, "unhealthy threshold");
                        Check.Equal(50, cfg.MaxParallelRequests, "max parallel");
                        Check.Equal(200, cfg.RateLimitRequestsThreshold, "rate limit");
                    }),

                    Case("AllEndpointProperties", "All endpoint properties round-trip through import", async ctx =>
                    {
                        ctx.Settings.Endpoints.Add(new ApiEndpoint
                        {
                            Identifier = "full-endpoint",
                            TimeoutMs = 45000,
                            LoadBalancing = LoadBalancingMode.Random,
                            BlockHttp10 = true,
                            MaxRequestBodySize = 10485760,
                            LogRequestFull = true,
                            LogRequestBody = true,
                            LogResponseBody = true,
                            IncludeAuthContextHeader = false,
                            AuthContextHeader = "x-custom-auth",
                            UseGlobalBlockedHeaders = false,
                            CaptureRequestBody = true,
                            CaptureResponseBody = true,
                            CaptureRequestHeaders = false,
                            CaptureResponseHeaders = false,
                            MaxCaptureRequestBodySize = 4096,
                            MaxCaptureResponseBodySize = 8192
                        });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        await svc.ImportAsync();

                        ApiEndpointConfig? cfg = await ctx.Client.ApiEndpoints.GetByIdentifierAsync("full-endpoint");
                        Check.True(cfg != null, "endpoint present");
                        Check.Equal(45000, cfg!.TimeoutMs, "timeout");
                        Check.Equal("Random", cfg.LoadBalancingMode, "lb mode string");
                        Check.True(cfg.BlockHttp10, "block http10");
                        Check.Equal(10485760, cfg.MaxRequestBodySize, "max body");
                        Check.Equal("x-custom-auth", cfg.AuthContextHeader, "auth header");
                        Check.False(cfg.IncludeAuthContextHeader, "include auth header");
                        Check.False(cfg.UseGlobalBlockedHeaders, "use global blocked headers");
                    }),

                    Case("Idempotency", "Second import skips all previously imported items", async ctx =>
                    {
                        ctx.Settings.Origins.Add(new OriginServer { Identifier = "origin", Hostname = "localhost", Port = 8001 });
                        ctx.Settings.Endpoints.Add(new ApiEndpoint { Identifier = "endpoint" });

                        SettingsImportService svc = new SettingsImportService(ctx.Settings, ctx.Client, _Logging);
                        ImportResult first = await svc.ImportAsync();
                        Check.Equal(1, first.OriginsImported, "first origins");
                        Check.Equal(1, first.EndpointsImported, "first endpoints");

                        ImportResult second = await svc.ImportAsync();
                        Check.Equal(0, second.OriginsImported, "second origins imported");
                        Check.Equal(0, second.EndpointsImported, "second endpoints imported");
                        Check.Equal(1, second.OriginsSkipped, "second origins skipped");
                        Check.Equal(1, second.EndpointsSkipped, "second endpoints skipped");
                    })
                });
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<ImportContext, Task> body)
        {
            return new TestCaseDescriptor(
                suiteId: "SettingsImport",
                caseId: caseId,
                displayName: displayName,
                executeAsync: async ct =>
                {
                    ImportContext ctx = await ImportContext.CreateAsync(ct).ConfigureAwait(false);
                    try
                    {
                        await body(ctx).ConfigureAwait(false);
                    }
                    finally
                    {
                        await ctx.DisposeAsync().ConfigureAwait(false);
                    }
                });
        }

        private sealed class ImportContext
        {
            public string DbPath { get; private set; } = string.Empty;
            public IDatabaseDriver Driver { get; private set; } = null!;
            public SwitchboardClient Client { get; private set; } = null!;
            public SwitchboardSettings Settings { get; private set; } = null!;

            public static async Task<ImportContext> CreateAsync(CancellationToken token)
            {
                string dbPath = Path.Combine(Path.GetTempPath(), "switchboard_test_" + Guid.NewGuid().ToString("N") + ".db");
                IDatabaseDriver driver = DatabaseDriverFactory.Create(DatabaseTypeEnum.Sqlite, "Data Source=" + dbPath);
                await driver.OpenAsync(token).ConfigureAwait(false);
                await driver.InitializeSchemaAsync(token).ConfigureAwait(false);

                return new ImportContext
                {
                    DbPath = dbPath,
                    Driver = driver,
                    Client = new SwitchboardClient(driver),
                    Settings = new SwitchboardSettings()
                };
            }

            public async Task DisposeAsync()
            {
                try
                {
                    Client?.Dispose();
                }
                catch (Exception)
                {
                    // ignore
                }

                try
                {
                    if (Driver != null) await Driver.CloseAsync().ConfigureAwait(false);
                    Driver?.Dispose();
                }
                catch (Exception)
                {
                    // ignore
                }

                try
                {
                    if (File.Exists(DbPath)) File.Delete(DbPath);
                }
                catch (Exception)
                {
                    // best-effort
                }
            }
        }
    }
}
