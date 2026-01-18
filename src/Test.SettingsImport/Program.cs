namespace Test.SettingsImport
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Switchboard.Core;
    using Switchboard.Core.Client;
    using Switchboard.Core.Database;
    using Switchboard.Core.Models;
    using Switchboard.Core.Services;
    using Switchboard.Core.Settings;
    using SyslogLogging;
    using WatsonWebserver.Core;

    public static class Program
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        private static bool _DebugMode = false;
        private static List<TestResult> _TestResults = new List<TestResult>();
        private static Stopwatch _OverallStopwatch = new Stopwatch();
        private static LoggingModule _Logging = null!;
        private static string _TestDbPath = null!;

        public static async Task Main(string[] args)
        {
            _OverallStopwatch.Start();

            Console.WriteLine("");
            Console.WriteLine("================================================");
            Console.WriteLine("   SETTINGS IMPORT SERVICE TEST SUITE");
            Console.WriteLine("================================================");
            Console.WriteLine("");
            Console.WriteLine("This test suite validates SettingsImportService:");
            Console.WriteLine("  - Initial import to empty database");
            Console.WriteLine("  - Skip existing items (no overwrite)");
            Console.WriteLine("  - Route import (authenticated/unauthenticated)");
            Console.WriteLine("  - Endpoint-origin mappings");
            Console.WriteLine("  - URL rewrite rules");
            Console.WriteLine("  - Global blocked headers");
            Console.WriteLine("  - Null identifier handling");
            Console.WriteLine("");

            // Initialize logging
            _Logging = new LoggingModule();
            _Logging.Settings.EnableConsole = _DebugMode;
            _Logging.Settings.MinimumSeverity = _DebugMode ? Severity.Debug : Severity.Alert;

            bool allTestsPassed = false;

            try
            {
                await RunTests();
                allTestsPassed = _TestResults.All(r => r.Passed);
            }
            catch (Exception ex)
            {
                Console.WriteLine("");
                Console.WriteLine("FATAL ERROR: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                _Logging?.Dispose();
            }

            _OverallStopwatch.Stop();
            PrintSummary();

            Environment.Exit(allTestsPassed ? 0 : 1);
        }

        private static async Task RunTests()
        {
            Console.WriteLine("Running tests...");
            Console.WriteLine("");

            // Test 1: Initial import to empty database
            await RunTest("Initial Import - Origins to Empty Database", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup: Add origins to settings
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

                    ctx.Settings.Origins.Add(new OriginServer
                    {
                        Identifier = "origin2",
                        Name = "Origin Server 2",
                        Hostname = "localhost",
                        Port = 8002,
                        Ssl = true
                    });

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    bool hasItems = await importService.HasItemsToImportAsync();
                    if (!hasItems)
                        throw new Exception("HasItemsToImportAsync should return true");

                    ImportResult result = await importService.ImportAsync();

                    // Assert
                    if (result.OriginsImported != 2)
                        throw new Exception("Expected 2 origins imported, got " + result.OriginsImported);

                    if (result.OriginsSkipped != 0)
                        throw new Exception("Expected 0 origins skipped, got " + result.OriginsSkipped);

                    // Verify in database
                    List<OriginServerConfig> dbOrigins = await ctx.Client.OriginServers.GetAllAsync();
                    if (dbOrigins.Count != 2)
                        throw new Exception("Expected 2 origins in database, got " + dbOrigins.Count);

                    OriginServerConfig? origin1 = await ctx.Client.OriginServers.GetByIdentifierAsync("origin1");
                    if (origin1 == null)
                        throw new Exception("Origin 'origin1' not found in database");

                    if (origin1.Name != "Origin Server 1")
                        throw new Exception("Origin name mismatch: expected 'Origin Server 1', got '" + origin1.Name + "'");

                    if (origin1.Hostname != "localhost")
                        throw new Exception("Origin hostname mismatch");

                    if (origin1.Port != 8001)
                        throw new Exception("Origin port mismatch");

                    if (origin1.HealthCheckUrl != "/health")
                        throw new Exception("Origin health check URL mismatch");

                    return "2 origins imported with all properties preserved";
                }
            });

            // Test 2: Initial import - Endpoints to empty database
            await RunTest("Initial Import - Endpoints to Empty Database", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup: Add origin first (endpoints reference it)
                    ctx.Settings.Origins.Add(new OriginServer
                    {
                        Identifier = "backend",
                        Name = "Backend Server",
                        Hostname = "localhost",
                        Port = 9000
                    });

                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "api-v1",
                        Name = "API Version 1",
                        TimeoutMs = 30000,
                        LoadBalancing = LoadBalancingMode.RoundRobin,
                        BlockHttp10 = true,
                        MaxRequestBodySize = 1024 * 1024,
                        IncludeAuthContextHeader = true,
                        AuthContextHeader = "x-auth-context",
                        UseGlobalBlockedHeaders = true,
                        OriginServers = new List<string> { "backend" }
                    });

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert
                    if (result.EndpointsImported != 1)
                        throw new Exception("Expected 1 endpoint imported, got " + result.EndpointsImported);

                    if (result.OriginsImported != 1)
                        throw new Exception("Expected 1 origin imported, got " + result.OriginsImported);

                    // Verify endpoint in database
                    ApiEndpointConfig? endpoint = await ctx.Client.ApiEndpoints.GetByIdentifierAsync("api-v1");
                    if (endpoint == null)
                        throw new Exception("Endpoint 'api-v1' not found in database");

                    if (endpoint.Name != "API Version 1")
                        throw new Exception("Endpoint name mismatch");

                    if (endpoint.TimeoutMs != 30000)
                        throw new Exception("Endpoint timeout mismatch");

                    if (endpoint.LoadBalancingMode != "RoundRobin")
                        throw new Exception("Endpoint load balancing mode mismatch: got " + endpoint.LoadBalancingMode);

                    if (!endpoint.BlockHttp10)
                        throw new Exception("Endpoint BlockHttp10 should be true");

                    if (endpoint.MaxRequestBodySize != 1024 * 1024)
                        throw new Exception("Endpoint MaxRequestBodySize mismatch");

                    return "1 endpoint imported with all properties preserved";
                }
            });

            // Test 3: Skip existing origins
            await RunTest("Skip Existing - Origins Already in Database", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup: Pre-create origin in database
                    OriginServerConfig existingOrigin = new OriginServerConfig("existing-origin")
                    {
                        Name = "Existing Origin (DB)",
                        Hostname = "db-host",
                        Port = 1234
                    };
                    await ctx.Client.OriginServers.CreateAsync(existingOrigin);

                    // Add same identifier to settings with different values
                    ctx.Settings.Origins.Add(new OriginServer
                    {
                        Identifier = "existing-origin",
                        Name = "Existing Origin (Settings)",
                        Hostname = "settings-host",
                        Port = 5678
                    });

                    // Add a new origin
                    ctx.Settings.Origins.Add(new OriginServer
                    {
                        Identifier = "new-origin",
                        Name = "New Origin",
                        Hostname = "new-host",
                        Port = 9999
                    });

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert
                    if (result.OriginsImported != 1)
                        throw new Exception("Expected 1 origin imported, got " + result.OriginsImported);

                    if (result.OriginsSkipped != 1)
                        throw new Exception("Expected 1 origin skipped, got " + result.OriginsSkipped);

                    // Verify existing origin was NOT overwritten
                    OriginServerConfig? dbOrigin = await ctx.Client.OriginServers.GetByIdentifierAsync("existing-origin");
                    if (dbOrigin == null)
                        throw new Exception("Existing origin not found");

                    if (dbOrigin.Name != "Existing Origin (DB)")
                        throw new Exception("Existing origin was overwritten! Name should be 'Existing Origin (DB)', got '" + dbOrigin.Name + "'");

                    if (dbOrigin.Hostname != "db-host")
                        throw new Exception("Existing origin hostname was overwritten!");

                    // Verify new origin was imported
                    OriginServerConfig? newOrigin = await ctx.Client.OriginServers.GetByIdentifierAsync("new-origin");
                    if (newOrigin == null)
                        throw new Exception("New origin was not imported");

                    return "Existing origin preserved, new origin imported";
                }
            });

            // Test 4: Skip existing endpoints
            await RunTest("Skip Existing - Endpoints Already in Database", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup: Pre-create endpoint in database
                    ApiEndpointConfig existingEndpoint = new ApiEndpointConfig("existing-endpoint")
                    {
                        Name = "Existing Endpoint (DB)",
                        TimeoutMs = 1000
                    };
                    await ctx.Client.ApiEndpoints.CreateAsync(existingEndpoint);

                    // Add same identifier to settings with different values
                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "existing-endpoint",
                        Name = "Existing Endpoint (Settings)",
                        TimeoutMs = 9999
                    });

                    // Add a new endpoint
                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "new-endpoint",
                        Name = "New Endpoint"
                    });

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert
                    if (result.EndpointsImported != 1)
                        throw new Exception("Expected 1 endpoint imported, got " + result.EndpointsImported);

                    if (result.EndpointsSkipped != 1)
                        throw new Exception("Expected 1 endpoint skipped, got " + result.EndpointsSkipped);

                    // Verify existing endpoint was NOT overwritten
                    ApiEndpointConfig? dbEndpoint = await ctx.Client.ApiEndpoints.GetByIdentifierAsync("existing-endpoint");
                    if (dbEndpoint == null)
                        throw new Exception("Existing endpoint not found");

                    if (dbEndpoint.TimeoutMs != 1000)
                        throw new Exception("Existing endpoint was overwritten! TimeoutMs should be 1000, got " + dbEndpoint.TimeoutMs);

                    return "Existing endpoint preserved, new endpoint imported";
                }
            });

            // Test 5: Unauthenticated routes import
            await RunTest("Route Import - Unauthenticated Routes", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup
                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "public-api",
                        Name = "Public API",
                        Unauthenticated = new ApiEndpointGroup
                        {
                            ParameterizedUrls = new Dictionary<string, List<string>>
                            {
                                { "GET", new List<string> { "/health", "/status", "/api/users/{id}" } },
                                { "POST", new List<string> { "/api/signup" } }
                            }
                        }
                    });

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert
                    if (result.RoutesImported != 4)
                        throw new Exception("Expected 4 routes imported, got " + result.RoutesImported);

                    // Verify routes in database (query by identifier since DB uses identifier, not GUID)
                    ApiEndpointConfig? endpoint = await ctx.Client.ApiEndpoints.GetByIdentifierAsync("public-api");
                    if (endpoint == null)
                        throw new Exception("Endpoint not found");

                    List<EndpointRoute> allRoutes = await ctx.Client.EndpointRoutes.GetAllAsync();
                    List<EndpointRoute> routes = allRoutes.Where(r => r.EndpointIdentifier == "public-api").ToList();
                    if (routes.Count != 4)
                        throw new Exception("Expected 4 routes in database, got " + routes.Count);

                    // Verify all routes are unauthenticated
                    foreach (EndpointRoute route in routes)
                    {
                        if (route.RequiresAuthentication)
                            throw new Exception("Route " + route.UrlPattern + " should not require authentication");
                    }

                    // Verify specific routes exist
                    bool hasHealthRoute = routes.Any(r => r.HttpMethod == "GET" && r.UrlPattern == "/health");
                    if (!hasHealthRoute)
                        throw new Exception("Missing GET /health route");

                    bool hasUserIdRoute = routes.Any(r => r.HttpMethod == "GET" && r.UrlPattern == "/api/users/{id}");
                    if (!hasUserIdRoute)
                        throw new Exception("Missing GET /api/users/{id} route");

                    bool hasSignupRoute = routes.Any(r => r.HttpMethod == "POST" && r.UrlPattern == "/api/signup");
                    if (!hasSignupRoute)
                        throw new Exception("Missing POST /api/signup route");

                    return "4 unauthenticated routes imported correctly";
                }
            });

            // Test 6: Authenticated routes import
            await RunTest("Route Import - Authenticated Routes", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup
                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "secure-api",
                        Name = "Secure API",
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

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert
                    if (result.RoutesImported != 4)
                        throw new Exception("Expected 4 routes imported, got " + result.RoutesImported);

                    // Verify routes in database (query by identifier since DB uses identifier, not GUID)
                    ApiEndpointConfig? endpoint = await ctx.Client.ApiEndpoints.GetByIdentifierAsync("secure-api");
                    List<EndpointRoute> allRoutes = await ctx.Client.EndpointRoutes.GetAllAsync();
                    List<EndpointRoute> routes = allRoutes.Where(r => r.EndpointIdentifier == "secure-api").ToList();

                    if (routes.Count != 4)
                        throw new Exception("Expected 4 routes in database, got " + routes.Count);

                    // Verify all routes require authentication
                    foreach (EndpointRoute route in routes)
                    {
                        if (!route.RequiresAuthentication)
                            throw new Exception("Route " + route.HttpMethod + " " + route.UrlPattern + " should require authentication");
                    }

                    return "4 authenticated routes imported correctly";
                }
            });

            // Test 7: Mixed authenticated and unauthenticated routes
            await RunTest("Route Import - Mixed Auth and Unauth Routes", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup
                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "mixed-api",
                        Name = "Mixed API",
                        Unauthenticated = new ApiEndpointGroup
                        {
                            ParameterizedUrls = new Dictionary<string, List<string>>
                            {
                                { "GET", new List<string> { "/public" } }
                            }
                        },
                        Authenticated = new ApiEndpointGroup
                        {
                            ParameterizedUrls = new Dictionary<string, List<string>>
                            {
                                { "GET", new List<string> { "/private" } }
                            }
                        }
                    });

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert (query by identifier since DB uses identifier, not GUID)
                    ApiEndpointConfig? endpoint = await ctx.Client.ApiEndpoints.GetByIdentifierAsync("mixed-api");
                    List<EndpointRoute> allRoutes = await ctx.Client.EndpointRoutes.GetAllAsync();
                    List<EndpointRoute> routes = allRoutes.Where(r => r.EndpointIdentifier == "mixed-api").ToList();

                    EndpointRoute? publicRoute = routes.FirstOrDefault(r => r.UrlPattern == "/public");
                    EndpointRoute? privateRoute = routes.FirstOrDefault(r => r.UrlPattern == "/private");

                    if (publicRoute == null)
                        throw new Exception("Public route not found");

                    if (privateRoute == null)
                        throw new Exception("Private route not found");

                    if (publicRoute.RequiresAuthentication)
                        throw new Exception("Public route should not require authentication");

                    if (!privateRoute.RequiresAuthentication)
                        throw new Exception("Private route should require authentication");

                    return "Mixed auth routes imported with correct RequiresAuthentication flags";
                }
            });

            // Test 8: Endpoint-origin mappings
            await RunTest("Origin Mapping - Endpoint to Multiple Origins", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup: Add origins
                    ctx.Settings.Origins.Add(new OriginServer
                    {
                        Identifier = "origin-a",
                        Name = "Origin A",
                        Hostname = "host-a",
                        Port = 8001
                    });

                    ctx.Settings.Origins.Add(new OriginServer
                    {
                        Identifier = "origin-b",
                        Name = "Origin B",
                        Hostname = "host-b",
                        Port = 8002
                    });

                    ctx.Settings.Origins.Add(new OriginServer
                    {
                        Identifier = "origin-c",
                        Name = "Origin C",
                        Hostname = "host-c",
                        Port = 8003
                    });

                    // Add endpoint referencing origins
                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "lb-endpoint",
                        Name = "Load Balanced Endpoint",
                        OriginServers = new List<string> { "origin-a", "origin-b", "origin-c" }
                    });

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert
                    if (result.MappingsImported != 3)
                        throw new Exception("Expected 3 mappings imported, got " + result.MappingsImported);

                    // Verify mappings in database (query by identifier since DB uses identifier, not GUID)
                    ApiEndpointConfig? endpoint = await ctx.Client.ApiEndpoints.GetByIdentifierAsync("lb-endpoint");
                    List<EndpointOriginMapping> allMappings = await ctx.Client.EndpointOriginMappings.GetAllAsync();
                    List<EndpointOriginMapping> mappings = allMappings.Where(m => m.EndpointIdentifier == "lb-endpoint").ToList();

                    if (mappings.Count != 3)
                        throw new Exception("Expected 3 mappings in database, got " + mappings.Count);

                    // Verify sort order is preserved
                    List<string> originIds = mappings.OrderBy(m => m.SortOrder).Select(m => m.OriginIdentifier).ToList();
                    if (originIds[0] != "origin-a" || originIds[1] != "origin-b" || originIds[2] != "origin-c")
                        throw new Exception("Origin sort order not preserved");

                    return "3 endpoint-origin mappings created with correct sort order";
                }
            });

            // Test 9: Mapping with missing origin reference
            await RunTest("Origin Mapping - Missing Origin Reference", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup: Add only one origin but reference two
                    ctx.Settings.Origins.Add(new OriginServer
                    {
                        Identifier = "exists",
                        Name = "Existing Origin",
                        Hostname = "localhost",
                        Port = 8000
                    });

                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "endpoint-with-missing",
                        Name = "Endpoint with Missing Origin",
                        OriginServers = new List<string> { "exists", "does-not-exist" }
                    });

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert - should import endpoint and only the valid mapping
                    if (result.EndpointsImported != 1)
                        throw new Exception("Expected 1 endpoint imported");

                    if (result.MappingsImported != 1)
                        throw new Exception("Expected 1 mapping imported (for existing origin), got " + result.MappingsImported);

                    return "Endpoint imported, invalid origin reference skipped with warning";
                }
            });

            // Test 10: URL rewrite rules
            await RunTest("URL Rewrites - Import Rewrite Rules", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup
                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "rewrite-endpoint",
                        Name = "Rewrite Endpoint",
                        RewriteUrls = new Dictionary<string, Dictionary<string, string>>
                        {
                            {
                                "GET", new Dictionary<string, string>
                                {
                                    { "/api/v1/users/{id}", "/users/{id}" },
                                    { "/api/v1/products/{id}", "/products/{id}" }
                                }
                            },
                            {
                                "POST", new Dictionary<string, string>
                                {
                                    { "/api/v1/users", "/users" }
                                }
                            }
                        }
                    });

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert
                    if (result.RewritesImported != 3)
                        throw new Exception("Expected 3 rewrites imported, got " + result.RewritesImported);

                    // Verify in database (query by identifier since DB uses identifier, not GUID)
                    ApiEndpointConfig? endpoint = await ctx.Client.ApiEndpoints.GetByIdentifierAsync("rewrite-endpoint");
                    List<UrlRewrite> allRewrites = await ctx.Client.UrlRewrites.GetAllAsync();
                    List<UrlRewrite> rewrites = allRewrites.Where(r => r.EndpointIdentifier == "rewrite-endpoint").ToList();

                    if (rewrites.Count != 3)
                        throw new Exception("Expected 3 rewrites in database, got " + rewrites.Count);

                    // Verify specific rewrite
                    UrlRewrite? userRewrite = rewrites.FirstOrDefault(r =>
                        r.HttpMethod == "GET" && r.SourcePattern == "/api/v1/users/{id}");

                    if (userRewrite == null)
                        throw new Exception("User rewrite not found");

                    if (userRewrite.TargetPattern != "/users/{id}")
                        throw new Exception("User rewrite target mismatch");

                    return "3 URL rewrite rules imported correctly";
                }
            });

            // Test 11: Global blocked headers
            await RunTest("Blocked Headers - Import Global Headers", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup
                    ctx.Settings.BlockedHeaders = new List<string>
                    {
                        "x-custom-header",
                        "x-internal-token",
                        "x-debug-info"
                    };

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert
                    if (result.BlockedHeadersImported != 3)
                        throw new Exception("Expected 3 blocked headers imported, got " + result.BlockedHeadersImported);

                    // Verify in database
                    List<BlockedHeader> headers = await ctx.Client.BlockedHeaders.GetAllAsync();
                    if (headers.Count != 3)
                        throw new Exception("Expected 3 blocked headers in database, got " + headers.Count);

                    // Headers are stored lowercase
                    bool hasCustomHeader = await ctx.Client.BlockedHeaders.IsBlockedAsync("x-custom-header");
                    if (!hasCustomHeader)
                        throw new Exception("x-custom-header should be blocked");

                    return "3 global blocked headers imported";
                }
            });

            // Test 12: Blocked headers - skip existing
            await RunTest("Blocked Headers - Skip Existing", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup: Pre-create a blocked header
                    await ctx.Client.BlockedHeaders.CreateAsync(new BlockedHeader("existing-header"));

                    ctx.Settings.BlockedHeaders = new List<string>
                    {
                        "existing-header",
                        "new-header"
                    };

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert - only new header should be imported
                    if (result.BlockedHeadersImported != 1)
                        throw new Exception("Expected 1 blocked header imported, got " + result.BlockedHeadersImported);

                    List<BlockedHeader> headers = await ctx.Client.BlockedHeaders.GetAllAsync();
                    if (headers.Count != 2)
                        throw new Exception("Expected 2 blocked headers in database, got " + headers.Count);

                    return "Existing blocked header preserved, new header imported";
                }
            });

            // Test 13: Null identifier handling - origins
            await RunTest("Null Handling - Origin with Null Identifier", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup: Add origin with null identifier (should be skipped)
                    ctx.Settings.Origins.Add(new OriginServer
                    {
                        Identifier = null!,
                        Name = "Invalid Origin",
                        Hostname = "localhost",
                        Port = 8000
                    });

                    // Add valid origin
                    ctx.Settings.Origins.Add(new OriginServer
                    {
                        Identifier = "valid-origin",
                        Name = "Valid Origin",
                        Hostname = "localhost",
                        Port = 8001
                    });

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert
                    if (result.OriginsImported != 1)
                        throw new Exception("Expected 1 origin imported, got " + result.OriginsImported);

                    List<OriginServerConfig> origins = await ctx.Client.OriginServers.GetAllAsync();
                    if (origins.Count != 1)
                        throw new Exception("Expected 1 origin in database, got " + origins.Count);

                    if (origins[0].Identifier != "valid-origin")
                        throw new Exception("Wrong origin imported");

                    return "Origin with null identifier skipped, valid origin imported";
                }
            });

            // Test 14: Null identifier handling - endpoints
            await RunTest("Null Handling - Endpoint with Null Identifier", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup: Add endpoint with null identifier (should be skipped)
                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = null!,
                        Name = "Invalid Endpoint"
                    });

                    // Add valid endpoint
                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "valid-endpoint",
                        Name = "Valid Endpoint"
                    });

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert
                    if (result.EndpointsImported != 1)
                        throw new Exception("Expected 1 endpoint imported, got " + result.EndpointsImported);

                    List<ApiEndpointConfig> endpoints = await ctx.Client.ApiEndpoints.GetAllAsync();
                    if (endpoints.Count != 1)
                        throw new Exception("Expected 1 endpoint in database, got " + endpoints.Count);

                    return "Endpoint with null identifier skipped, valid endpoint imported";
                }
            });

            // Test 15: HasItemsToImportAsync - empty settings
            await RunTest("HasItemsToImport - Empty Settings Returns False", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Settings has no endpoints or origins by default
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    bool hasItems = await importService.HasItemsToImportAsync();

                    if (hasItems)
                        throw new Exception("Expected HasItemsToImportAsync to return false for empty settings");

                    return "HasItemsToImportAsync correctly returns false for empty settings";
                }
            });

            // Test 16: HasItemsToImportAsync - with endpoints only
            await RunTest("HasItemsToImport - With Endpoints Returns True", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    ctx.Settings.Endpoints.Add(new ApiEndpoint { Identifier = "test" });

                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    bool hasItems = await importService.HasItemsToImportAsync();

                    if (!hasItems)
                        throw new Exception("Expected HasItemsToImportAsync to return true when endpoints exist");

                    return "HasItemsToImportAsync correctly returns true when endpoints exist";
                }
            });

            // Test 17: HasItemsToImportAsync - with origins only
            await RunTest("HasItemsToImport - With Origins Returns True", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    ctx.Settings.Origins.Add(new OriginServer
                    {
                        Identifier = "test",
                        Hostname = "localhost",
                        Port = 8000
                    });

                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    bool hasItems = await importService.HasItemsToImportAsync();

                    if (!hasItems)
                        throw new Exception("Expected HasItemsToImportAsync to return true when origins exist");

                    return "HasItemsToImportAsync correctly returns true when origins exist";
                }
            });

            // Test 18: Empty route lists
            await RunTest("Empty Routes - Endpoint with No Routes", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup: Endpoint with null/empty route dictionaries
                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "empty-routes",
                        Name = "Empty Routes Endpoint",
                        Unauthenticated = new ApiEndpointGroup
                        {
                            ParameterizedUrls = new Dictionary<string, List<string>>() // empty
                        },
                        Authenticated = null! // null
                    });

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult result = await importService.ImportAsync();

                    // Assert - should import endpoint without error
                    if (result.EndpointsImported != 1)
                        throw new Exception("Expected 1 endpoint imported, got " + result.EndpointsImported);

                    if (result.RoutesImported != 0)
                        throw new Exception("Expected 0 routes imported, got " + result.RoutesImported);

                    return "Endpoint with empty routes imported without error";
                }
            });

            // Test 19: Full property mapping - origin
            await RunTest("Property Mapping - All Origin Properties", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup: Origin with all properties set
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

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    await importService.ImportAsync();

                    // Assert - verify all properties
                    OriginServerConfig? origin = await ctx.Client.OriginServers.GetByIdentifierAsync("full-origin");

                    if (origin == null)
                        throw new Exception("Origin not found");

                    if (origin.Hostname != "example.com") throw new Exception("Hostname mismatch");
                    if (origin.Port != 443) throw new Exception("Port mismatch");
                    if (!origin.Ssl) throw new Exception("Ssl mismatch");
                    if (origin.HealthCheckIntervalMs != 10000) throw new Exception("HealthCheckIntervalMs mismatch");
                    if (origin.HealthCheckMethod != "HEAD") throw new Exception("HealthCheckMethod mismatch: " + origin.HealthCheckMethod);
                    if (origin.HealthCheckUrl != "/healthz") throw new Exception("HealthCheckUrl mismatch");
                    if (origin.UnhealthyThreshold != 5) throw new Exception("UnhealthyThreshold mismatch");
                    if (origin.HealthyThreshold != 3) throw new Exception("HealthyThreshold mismatch");
                    if (origin.MaxParallelRequests != 50) throw new Exception("MaxParallelRequests mismatch");
                    if (origin.RateLimitRequestsThreshold != 200) throw new Exception("RateLimitRequestsThreshold mismatch");
                    if (!origin.LogRequestBody) throw new Exception("LogRequestBody mismatch");
                    if (!origin.LogResponseBody) throw new Exception("LogResponseBody mismatch");
                    if (!origin.CaptureRequestBody) throw new Exception("CaptureRequestBody mismatch");
                    if (!origin.CaptureResponseBody) throw new Exception("CaptureResponseBody mismatch");
                    if (origin.CaptureRequestHeaders) throw new Exception("CaptureRequestHeaders mismatch");
                    if (origin.CaptureResponseHeaders) throw new Exception("CaptureResponseHeaders mismatch");
                    if (origin.MaxCaptureRequestBodySize != 1024) throw new Exception("MaxCaptureRequestBodySize mismatch");
                    if (origin.MaxCaptureResponseBodySize != 2048) throw new Exception("MaxCaptureResponseBodySize mismatch");

                    return "All origin properties mapped correctly";
                }
            });

            // Test 20: Full property mapping - endpoint
            await RunTest("Property Mapping - All Endpoint Properties", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup: Endpoint with all properties set
                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "full-endpoint",
                        Name = "Full Endpoint",
                        TimeoutMs = 45000,
                        LoadBalancing = LoadBalancingMode.Random,
                        BlockHttp10 = true,
                        MaxRequestBodySize = 10 * 1024 * 1024,
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

                    // Act
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    await importService.ImportAsync();

                    // Assert - verify all properties
                    ApiEndpointConfig? endpoint = await ctx.Client.ApiEndpoints.GetByIdentifierAsync("full-endpoint");

                    if (endpoint == null)
                        throw new Exception("Endpoint not found");

                    if (endpoint.TimeoutMs != 45000) throw new Exception("TimeoutMs mismatch");
                    if (endpoint.LoadBalancingMode != "Random") throw new Exception("LoadBalancingMode mismatch: " + endpoint.LoadBalancingMode);
                    if (!endpoint.BlockHttp10) throw new Exception("BlockHttp10 mismatch");
                    if (endpoint.MaxRequestBodySize != 10 * 1024 * 1024) throw new Exception("MaxRequestBodySize mismatch");
                    if (!endpoint.LogRequestFull) throw new Exception("LogRequestFull mismatch");
                    if (!endpoint.LogRequestBody) throw new Exception("LogRequestBody mismatch");
                    if (!endpoint.LogResponseBody) throw new Exception("LogResponseBody mismatch");
                    if (endpoint.IncludeAuthContextHeader) throw new Exception("IncludeAuthContextHeader mismatch");
                    if (endpoint.AuthContextHeader != "x-custom-auth") throw new Exception("AuthContextHeader mismatch");
                    if (endpoint.UseGlobalBlockedHeaders) throw new Exception("UseGlobalBlockedHeaders mismatch");
                    if (!endpoint.CaptureRequestBody) throw new Exception("CaptureRequestBody mismatch");
                    if (!endpoint.CaptureResponseBody) throw new Exception("CaptureResponseBody mismatch");
                    if (endpoint.CaptureRequestHeaders) throw new Exception("CaptureRequestHeaders mismatch");
                    if (endpoint.CaptureResponseHeaders) throw new Exception("CaptureResponseHeaders mismatch");
                    if (endpoint.MaxCaptureRequestBodySize != 4096) throw new Exception("MaxCaptureRequestBodySize mismatch");
                    if (endpoint.MaxCaptureResponseBodySize != 8192) throw new Exception("MaxCaptureResponseBodySize mismatch");

                    return "All endpoint properties mapped correctly";
                }
            });

            // Test 21: Second import has nothing new
            await RunTest("Idempotency - Second Import Skips All", async () =>
            {
                using (TestContext ctx = await CreateTestContext())
                {
                    // Setup
                    ctx.Settings.Origins.Add(new OriginServer
                    {
                        Identifier = "origin",
                        Hostname = "localhost",
                        Port = 8000
                    });

                    ctx.Settings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "endpoint",
                        Name = "Endpoint"
                    });

                    // First import
                    SettingsImportService importService = new SettingsImportService(
                        ctx.Settings, ctx.Client, _Logging);

                    ImportResult firstResult = await importService.ImportAsync();

                    if (firstResult.OriginsImported != 1 || firstResult.EndpointsImported != 1)
                        throw new Exception("First import should import items");

                    // Second import with same settings
                    ImportResult secondResult = await importService.ImportAsync();

                    if (secondResult.OriginsImported != 0)
                        throw new Exception("Second import should not import any origins, got " + secondResult.OriginsImported);

                    if (secondResult.EndpointsImported != 0)
                        throw new Exception("Second import should not import any endpoints, got " + secondResult.EndpointsImported);

                    if (secondResult.OriginsSkipped != 1)
                        throw new Exception("Second import should skip 1 origin, got " + secondResult.OriginsSkipped);

                    if (secondResult.EndpointsSkipped != 1)
                        throw new Exception("Second import should skip 1 endpoint, got " + secondResult.EndpointsSkipped);

                    return "Second import correctly skipped all existing items";
                }
            });
        }

        private static async Task<TestContext> CreateTestContext()
        {
            // Create unique database file for this test
            string dbPath = Path.Combine(Path.GetTempPath(), "switchboard_test_" + Guid.NewGuid().ToString("N") + ".db");
            _TestDbPath = dbPath;

            // Create database driver
            IDatabaseDriver driver = DatabaseDriverFactory.Create(DatabaseTypeEnum.Sqlite, "Data Source=" + dbPath);
            await driver.OpenAsync();
            await driver.InitializeSchemaAsync();

            // Create client
            SwitchboardClient client = new SwitchboardClient(driver);

            // Create settings
            SwitchboardSettings settings = new SwitchboardSettings();

            return new TestContext
            {
                DbPath = dbPath,
                Driver = driver,
                Client = client,
                Settings = settings
            };
        }

        private static async Task RunTest(string testName, Func<Task<string>> testAction)
        {
            Console.Write("  [" + (_TestResults.Count + 1).ToString().PadLeft(2) + "] " + testName.PadRight(50) + " ... ");

            Stopwatch sw = Stopwatch.StartNew();
            TestResult result = new TestResult
            {
                TestName = testName,
                StartTime = DateTime.Now
            };

            try
            {
                result.ResultMessage = await testAction();
                result.Passed = true;
                sw.Stop();
                result.Duration = sw.Elapsed;

                Console.WriteLine("PASS (" + result.Duration.TotalMilliseconds.ToString("F0") + "ms)");
                if (_DebugMode && !String.IsNullOrEmpty(result.ResultMessage))
                {
                    Console.WriteLine("       -> " + result.ResultMessage);
                }
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.ErrorMessage = ex.Message;
                sw.Stop();
                result.Duration = sw.Elapsed;

                Console.WriteLine("FAIL");
                Console.WriteLine("       Error: " + ex.Message);

                if (_DebugMode && ex.StackTrace != null)
                {
                    string[] lines = ex.StackTrace.Split('\n');
                    foreach (string line in lines.Take(3))
                    {
                        Console.WriteLine("       " + line.Trim());
                    }
                }
            }

            _TestResults.Add(result);
        }

        private static void PrintSummary()
        {
            Console.WriteLine("");
            Console.WriteLine("================================================");
            Console.WriteLine("                TEST SUMMARY");
            Console.WriteLine("================================================");
            Console.WriteLine("");

            int passed = _TestResults.Count(r => r.Passed);
            int failed = _TestResults.Count(r => !r.Passed);

            Console.WriteLine("Total Tests:   " + _TestResults.Count);
            Console.WriteLine("Passed:        " + passed + " (" + (passed * 100.0 / _TestResults.Count).ToString("F1") + "%)");
            Console.WriteLine("Failed:        " + failed);
            Console.WriteLine("Total Runtime: " + _OverallStopwatch.Elapsed.TotalSeconds.ToString("F2") + " seconds");
            Console.WriteLine("");

            if (failed > 0)
            {
                Console.WriteLine("Failed Tests:");
                foreach (TestResult testResult in _TestResults.Where(r => !r.Passed))
                {
                    Console.WriteLine("  X " + testResult.TestName);
                    Console.WriteLine("    Error: " + testResult.ErrorMessage);
                }
                Console.WriteLine("");
            }

            if (failed == 0)
            {
                Console.WriteLine("ALL TESTS PASSED");
            }
            else
            {
                Console.WriteLine(failed + " TEST(S) FAILED");
            }

            Console.WriteLine("================================================");
            Console.WriteLine("");
        }

        private class TestContext : IDisposable
        {
            public string DbPath { get; set; } = string.Empty;
            public IDatabaseDriver Driver { get; set; } = null!;
            public SwitchboardClient Client { get; set; } = null!;
            public SwitchboardSettings Settings { get; set; } = null!;

            public void Dispose()
            {
                Client?.Dispose();
                Driver?.Dispose();

                // Clean up database file
                if (!String.IsNullOrEmpty(DbPath) && File.Exists(DbPath))
                {
                    try
                    {
                        File.Delete(DbPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        private class TestResult
        {
            public string TestName { get; set; } = string.Empty;
            public bool Passed { get; set; }
            public DateTime StartTime { get; set; }
            public TimeSpan Duration { get; set; }
            public string ResultMessage { get; set; } = string.Empty;
            public string ErrorMessage { get; set; } = string.Empty;
        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
