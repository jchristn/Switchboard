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
    using SyslogLogging;
    using Touchstone.Core;

    /// <summary>
    /// Exercises the deterministic-GUID resolution for origins/endpoints and the
    /// <see cref="ConfigurationReloadService"/> projection of database-managed configuration into
    /// live settings. Each case is self-contained (its own temporary SQLite database) and binds no
    /// ports, so it runs as part of the network-free unit set.
    /// </summary>
    public static class ConfigurationReloadSuites
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
        /// The configuration-reload suite.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the configuration-reload suite descriptor.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ConfigurationReload",
                displayName: "Configuration Reload Service",
                cases: new List<TestCaseDescriptor>
                {
                    Case("OriginGuidStableAndResolvable", "Origin GUID is stable and resolvable by GUID", async ctx =>
                    {
                        OriginServerConfig created = await ctx.Client.OriginServers.CreateAsync(
                            new OriginServerConfig { Identifier = "o1", Hostname = "localhost", Port = 8001 });

                        OriginServerConfig? read1 = await ctx.Client.OriginServers.GetByIdentifierAsync("o1");
                        OriginServerConfig? read2 = await ctx.Client.OriginServers.GetByIdentifierAsync("o1");
                        Check.True(read1 != null && read2 != null, "reads returned");
                        Check.Equal(created.GUID, read1!.GUID, "guid stable across create/read");
                        Check.Equal(read1.GUID, read2!.GUID, "guid stable across reads");
                        Check.True(read1.GUID != Guid.Empty, "guid non-empty");

                        OriginServerConfig? byGuid = await ctx.Client.OriginServers.GetByGuidAsync(created.GUID);
                        Check.True(byGuid != null, "resolved by guid");
                        Check.Equal("o1", byGuid!.Identifier, "resolved identifier");
                        Check.True(await ctx.Client.OriginServers.ExistsByGuidAsync(created.GUID), "exists by guid");

                        await ctx.Client.OriginServers.DeleteByGuidAsync(created.GUID);
                        Check.True(await ctx.Client.OriginServers.GetByIdentifierAsync("o1") == null, "deleted by guid");
                    }),

                    Case("EndpointGuidStableAndResolvable", "Endpoint GUID is stable and resolvable by GUID", async ctx =>
                    {
                        ApiEndpointConfig created = await ctx.Client.ApiEndpoints.CreateAsync(
                            new ApiEndpointConfig { Identifier = "e1", Name = "Endpoint 1" });

                        ApiEndpointConfig? read = await ctx.Client.ApiEndpoints.GetByIdentifierAsync("e1");
                        Check.True(read != null, "read returned");
                        Check.Equal(created.GUID, read!.GUID, "guid stable");
                        Check.True(read.GUID != Guid.Empty, "guid non-empty");

                        ApiEndpointConfig? byGuid = await ctx.Client.ApiEndpoints.GetByGuidAsync(created.GUID);
                        Check.True(byGuid != null, "resolved by guid");
                        Check.Equal("e1", byGuid!.Identifier, "resolved identifier");

                        await ctx.Client.ApiEndpoints.DeleteByGuidAsync(created.GUID);
                        Check.True(await ctx.Client.ApiEndpoints.GetByIdentifierAsync("e1") == null, "deleted by guid");
                    }),

                    Case("ProjectsDatabaseEndpointIntoSettings", "Reload projects a database-only endpoint into settings", async ctx =>
                    {
                        // Seed the database as the management API/dashboard would.
                        OriginServerConfig origin = await ctx.Client.OriginServers.CreateAsync(
                            new OriginServerConfig { Identifier = "backend", Hostname = "localhost", Port = 9000, Ssl = false });

                        ApiEndpointConfig endpoint = await ctx.Client.ApiEndpoints.CreateAsync(
                            new ApiEndpointConfig { Identifier = "foo-endpoint", Name = "Foo", LoadBalancingMode = "RoundRobin" });

                        await ctx.Client.EndpointRoutes.CreateAsync(
                            new EndpointRoute("foo-endpoint", "GET", "/foo", requiresAuthentication: false) { EndpointGUID = endpoint.GUID });
                        await ctx.Client.EndpointRoutes.CreateAsync(
                            new EndpointRoute("foo-endpoint", "POST", "/bar", requiresAuthentication: true) { EndpointGUID = endpoint.GUID });

                        await ctx.Client.UrlRewrites.CreateAsync(
                            new UrlRewrite("foo-endpoint", "GET", "/foo", "/internal/foo") { EndpointGUID = endpoint.GUID });

                        await ctx.Client.EndpointOriginMappings.CreateAsync(
                            new EndpointOriginMapping("foo-endpoint", "backend", 0) { EndpointGUID = endpoint.GUID, OriginGUID = origin.GUID });

                        // Baseline settings are empty (no configuration file items).
                        SwitchboardSettings settings = new SwitchboardSettings();
                        using (ConfigurationReloadService svc = new ConfigurationReloadService(settings, ctx.Client, _Logging))
                        {
                            bool changed = await svc.ReloadAsync();
                            Check.True(changed, "reload reported a change");

                            // Origin projected.
                            Check.Equal(1, settings.Origins.Count, "origin count");
                            Check.Equal("backend", settings.Origins[0].Identifier, "origin identifier");
                            Check.Equal(9000, settings.Origins[0].Port, "origin port");

                            // Endpoint projected with routes grouped by auth requirement.
                            ApiEndpoint? ep = settings.Endpoints.FirstOrDefault(e => e.Identifier == "foo-endpoint");
                            Check.True(ep != null, "endpoint projected");
                            Check.True(ep!.Unauthenticated.ParameterizedUrls.ContainsKey("GET"), "has unauth GET");
                            Check.True(ep.Unauthenticated.ParameterizedUrls["GET"].Contains("/foo"), "unauth GET /foo present");
                            Check.True(ep.Authenticated.ParameterizedUrls.ContainsKey("POST"), "has auth POST");
                            Check.True(ep.Authenticated.ParameterizedUrls["POST"].Contains("/bar"), "auth POST /bar present");

                            // Origin mapping projected.
                            Check.True(ep.OriginServers.Contains("backend"), "origin mapped to endpoint");

                            // Rewrite projected.
                            Check.True(ep.RewriteUrls.ContainsKey("GET") && ep.RewriteUrls["GET"]["/foo"] == "/internal/foo", "rewrite projected");

                            // Second reload with no change is a no-op.
                            bool changedAgain = await svc.ReloadAsync();
                            Check.False(changedAgain, "second reload is a no-op");
                        }
                    }),

                    Case("BaselineEndpointsPreservedAndMerged", "Baseline endpoints are preserved and database-only endpoints are merged", async ctx =>
                    {
                        // A database-only endpoint (as if created via the dashboard).
                        ApiEndpointConfig dbEndpoint = await ctx.Client.ApiEndpoints.CreateAsync(
                            new ApiEndpointConfig { Identifier = "db-endpoint", Name = "From DB" });
                        await ctx.Client.EndpointRoutes.CreateAsync(
                            new EndpointRoute("db-endpoint", "GET", "/db", requiresAuthentication: false) { EndpointGUID = dbEndpoint.GUID });

                        // A baseline endpoint (as if loaded from the configuration file).
                        SwitchboardSettings settings = new SwitchboardSettings();
                        ApiEndpoint baseline = new ApiEndpoint { Identifier = "file-endpoint", Name = "From File" };
                        baseline.Unauthenticated.ParameterizedUrls["GET"] = new List<string> { "/file" };
                        settings.Endpoints.Add(baseline);

                        using (ConfigurationReloadService svc = new ConfigurationReloadService(settings, ctx.Client, _Logging))
                        {
                            await svc.ReloadAsync();

                            Check.Equal(2, settings.Endpoints.Count, "both endpoints present");

                            ApiEndpoint? file = settings.Endpoints.FirstOrDefault(e => e.Identifier == "file-endpoint");
                            Check.True(file != null, "baseline endpoint present");
                            Check.True(ReferenceEquals(file, baseline), "baseline instance identity preserved");

                            ApiEndpoint? db = settings.Endpoints.FirstOrDefault(e => e.Identifier == "db-endpoint");
                            Check.True(db != null, "database endpoint merged");
                            Check.True(db!.Unauthenticated.ParameterizedUrls["GET"].Contains("/db"), "database endpoint route projected");
                        }
                    }),
                });
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<ReloadContext, Task> body)
        {
            return new TestCaseDescriptor(
                suiteId: "ConfigurationReload",
                caseId: caseId,
                displayName: displayName,
                executeAsync: async ct =>
                {
                    ReloadContext ctx = await ReloadContext.CreateAsync(ct).ConfigureAwait(false);
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

        private sealed class ReloadContext
        {
            public string DbPath { get; private set; } = string.Empty;
            public IDatabaseDriver Driver { get; private set; } = null!;
            public SwitchboardClient Client { get; private set; } = null!;

            public static async Task<ReloadContext> CreateAsync(CancellationToken token)
            {
                string dbPath = Path.Combine(Path.GetTempPath(), "switchboard_test_" + Guid.NewGuid().ToString("N") + ".db");
                IDatabaseDriver driver = DatabaseDriverFactory.Create(DatabaseTypeEnum.Sqlite, "Data Source=" + dbPath);
                await driver.OpenAsync(token).ConfigureAwait(false);
                await driver.InitializeSchemaAsync(token).ConfigureAwait(false);

                return new ReloadContext
                {
                    DbPath = dbPath,
                    Driver = driver,
                    Client = new SwitchboardClient(driver)
                };
            }

            public async Task DisposeAsync()
            {
                try { Client?.Dispose(); }
                catch (Exception) { /* ignore */ }

                try
                {
                    if (Driver != null) await Driver.CloseAsync().ConfigureAwait(false);
                    Driver?.Dispose();
                }
                catch (Exception) { /* ignore */ }

                try
                {
                    if (File.Exists(DbPath)) File.Delete(DbPath);
                }
                catch (Exception) { /* best-effort */ }
            }
        }
    }
}
