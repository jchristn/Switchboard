namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;

    using RestWrapper;
    using Switchboard.Core.Models;
    using Switchboard.Core.Services;
    using Test.Shared.Harness;
    using Touchstone.Core;

    using HttpMethod = System.Net.Http.HttpMethod;

    /// <summary>
    /// Integration suite for the management REST API (<c>/_sb/v1.0/…</c>): admin-token authentication
    /// and read access to imported origins and endpoints. Runs against a dedicated harness that has
    /// the management API enabled.
    /// </summary>
    public static class ManagementApiSuites
    {
        private const string AdminToken = "sbadmin";

        // Responses mix PascalCase (resource models) and camelCase (computed endpoints); case-insensitive
        // matching lets a single named type deserialize either.
        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        /// <summary>
        /// All management API suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the management API suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Management",
                displayName: "Management API",
                beforeSuiteAsync: ct => new System.Threading.Tasks.ValueTask(TestHarnesses.Management.StartAsync(ct)),
                cases: new List<TestCaseDescriptor>
                {
                    Case("ListOriginsAuthorized", "GET /origins with admin token returns imported origins", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/_sb/v1.0/origins")))
                        {
                            req.Authorization.BearerToken = AdminToken;
                            using (RestResponse resp = await req.SendAsync())
                            {
                                Check.Equal(200, resp.StatusCode, "authorized list status");
                                Check.Contains(resp.DataAsString, "Server 1", "origin identifier present");
                            }
                        }
                    }),

                    Case("ListOriginsUnauthorized", "GET /origins without a token returns 401", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/_sb/v1.0/origins")))
                        using (RestResponse resp = await req.SendAsync())
                            Check.Equal(401, resp.StatusCode, "unauthorized list status");
                    }),

                    Case("ListOriginsBadToken", "GET /origins with a wrong token returns 401", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/_sb/v1.0/origins")))
                        {
                            req.Authorization.BearerToken = "not-the-admin-token";
                            using (RestResponse resp = await req.SendAsync())
                                Check.Equal(401, resp.StatusCode, "bad token status");
                        }
                    }),

                    Case("ListEndpointsAuthorized", "GET /endpoints with admin token returns imported endpoints", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/_sb/v1.0/endpoints")))
                        {
                            req.Authorization.BearerToken = AdminToken;
                            using (RestResponse resp = await req.SendAsync())
                            {
                                Check.Equal(200, resp.StatusCode, "authorized endpoints status");
                                Check.Contains(resp.DataAsString, "test-endpoint", "endpoint identifier present");
                            }
                        }
                    }),

                    Case("TimeSeriesBucketing", "GET /history/timeseries aggregates rows into fixed, zero-filled buckets", async (h, ct) =>
                    {
                        // Request history rows are timestamped by the store at insert time, so the window
                        // is anchored around 'now'. The pure multi-bucket zero-fill behaviour is covered by
                        // the BuildTimeSeries unit case below.
                        DateTime windowStart = DateTime.UtcNow.AddMinutes(-3);

                        await InsertHistoryRow(h, true, 100, ct).ConfigureAwait(false);
                        await InsertHistoryRow(h, false, 300, ct).ConfigureAwait(false);
                        await InsertHistoryRow(h, true, 200, ct).ConfigureAwait(false);

                        DateTime windowEnd = DateTime.UtcNow.AddMinutes(3);
                        string start = windowStart.ToString("yyyy-MM-ddTHH:mm:ssZ");
                        string end = windowEnd.ToString("yyyy-MM-ddTHH:mm:ssZ");

                        string url = h.Url("/_sb/v1.0/history/timeseries?start=" + start + "&end=" + end + "&intervalMinutes=60");
                        using (RestRequest req = new RestRequest(url))
                        {
                            req.Authorization.BearerToken = AdminToken;
                            using (RestResponse resp = await req.SendAsync())
                            {
                                Check.Equal(200, resp.StatusCode, "timeseries status");

                                TimeSeriesResponse ts = JsonSerializer.Deserialize<TimeSeriesResponse>(resp.DataAsString, _Json)!;
                                Check.True(ts.Buckets.Count >= 1, "bucket count");

                                // All inserted rows fall in the single hour-wide bucket.
                                long total = 0, success = 0, failure = 0;
                                double weightedDuration = 0;
                                foreach (TimeSeriesBucket b in ts.Buckets)
                                {
                                    total += b.Total;
                                    success += b.Success;
                                    failure += b.Failure;
                                    weightedDuration += b.AvgDurationMs * b.Total;
                                }

                                Check.True(total >= 3, "aggregated total");
                                Check.True(success >= 2, "aggregated success");
                                Check.True(failure >= 1, "aggregated failure");
                                Check.True(weightedDuration > 0, "aggregated avg duration");
                            }
                        }
                    }),

                    Case("TimeSeriesUnauthorized", "GET /history/timeseries without a token returns 401", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/_sb/v1.0/history/timeseries")))
                        using (RestResponse resp = await req.SendAsync())
                            Check.Equal(401, resp.StatusCode, "timeseries unauthorized status");
                    }),

                    Case("TimeSeriesUnitAggregation", "BuildTimeSeries buckets rows and zero-fills gaps", async (h, ct) =>
                    {
                        DateTime start = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        DateTime end = start.AddHours(3);

                        List<RequestHistory> rows = new List<RequestHistory>
                        {
                            new RequestHistory { TimestampUtc = start.AddMinutes(1), Success = true, DurationMs = 10 },
                            new RequestHistory { TimestampUtc = start.AddMinutes(30), Success = false, DurationMs = 30 },
                            new RequestHistory { TimestampUtc = start.AddMinutes(150), Success = true, DurationMs = 50 }
                        };

                        List<TimeSeriesBucket> buckets = ManagementService.BuildTimeSeries(rows, start, end, 60);

                        Check.Equal(3, buckets.Count, "bucket count");
                        Check.Equal(2L, buckets[0].Total, "bucket 0 total");
                        Check.Equal(1L, buckets[0].Success, "bucket 0 success");
                        Check.Equal(1L, buckets[0].Failure, "bucket 0 failure");
                        Check.Equal(20d, buckets[0].AvgDurationMs, "bucket 0 avg");
                        Check.Equal(0L, buckets[1].Total, "bucket 1 zero-filled");
                        Check.Equal(1L, buckets[2].Total, "bucket 2 total");
                    }),

                    Case("SettingsGetMasked", "GET /settings masks secrets and includes restart metadata", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/_sb/v1.0/settings")))
                        {
                            req.Authorization.BearerToken = AdminToken;
                            using (RestResponse resp = await req.SendAsync())
                            {
                                Check.Equal(200, resp.StatusCode, "settings get status");

                                SettingsResponse settings = JsonSerializer.Deserialize<SettingsResponse>(resp.DataAsString, _Json)!;
                                Check.Equal("********", settings.Management.AdminToken, "admin token masked");
                                Check.False(resp.DataAsString.Contains(AdminToken), "raw admin token not present");
                                Check.True(settings.RestartRequiredSettings.Length > 0, "restart list present");
                                Check.True(settings.RuntimeEditableSettings.Length > 0, "runtime list present");
                            }
                        }
                    }),

                    Case("SettingsPutRoundTrip", "PUT /settings applies a hot-swappable field live and preserves masked secrets", async (h, ct) =>
                    {
                        SettingsResponse settings;
                        using (RestRequest getReq = new RestRequest(h.Url("/_sb/v1.0/settings")))
                        {
                            getReq.Authorization.BearerToken = AdminToken;
                            using (RestResponse getResp = await getReq.SendAsync())
                                settings = JsonSerializer.Deserialize<SettingsResponse>(getResp.DataAsString, _Json)!;
                        }

                        // Mutate a runtime-editable field and round-trip the masked admin token unchanged.
                        settings.Logging.MinimumSeverity = 4;

                        using (RestRequest putReq = new RestRequest(h.Url("/_sb/v1.0/settings"), HttpMethod.Put))
                        {
                            putReq.Authorization.BearerToken = AdminToken;
                            putReq.ContentType = "application/json";
                            using (RestResponse putResp = await putReq.SendAsync(JsonSerializer.Serialize(settings, _Json)))
                                Check.Equal(200, putResp.StatusCode, "settings put status");
                        }

                        // A subsequent authorized GET proves the admin token was preserved (masked value not stored)
                        // and reflects the live-applied severity change.
                        using (RestRequest verifyReq = new RestRequest(h.Url("/_sb/v1.0/settings")))
                        {
                            verifyReq.Authorization.BearerToken = AdminToken;
                            using (RestResponse verifyResp = await verifyReq.SendAsync())
                            {
                                Check.Equal(200, verifyResp.StatusCode, "settings verify status");
                                SettingsResponse verified = JsonSerializer.Deserialize<SettingsResponse>(verifyResp.DataAsString, _Json)!;
                                Check.Equal(4, verified.Logging.MinimumSeverity, "severity applied live");
                            }
                        }
                    }),

                    Case("RestartReturns202", "POST /system/restart returns 202 and is admin-gated (test seam prevents exit)", async (h, ct) =>
                    {
                        // Substitute a no-op restart action so the endpoint does not kill the test process.
                        h.Daemon!.ManagementService.RestartAction = () => { };

                        using (RestRequest noAuth = new RestRequest(h.Url("/_sb/v1.0/system/restart"), HttpMethod.Post))
                        using (RestResponse resp = await noAuth.SendAsync())
                            Check.Equal(401, resp.StatusCode, "restart unauthorized status");

                        using (RestRequest req = new RestRequest(h.Url("/_sb/v1.0/system/restart"), HttpMethod.Post))
                        {
                            req.Authorization.BearerToken = AdminToken;
                            using (RestResponse resp = await req.SendAsync())
                                Check.Equal(202, resp.StatusCode, "restart accepted status");
                        }
                    }),

                    Case("ValidateCatchesMissingOrigin", "POST /config/validate flags an endpoint referencing a missing origin", async (h, ct) =>
                    {
                        ConfigValidationRequest bad = new ConfigValidationRequest
                        {
                            Origins = new List<OriginServerConfig> { new OriginServerConfig("o1") { Hostname = "localhost", Port = 1 } },
                            Endpoints = new List<ApiEndpointConfig> { new ApiEndpointConfig("e1") },
                            Routes = new List<EndpointRoute> { new EndpointRoute("e1", "GET", "/x") },
                            Mappings = new List<EndpointOriginMapping> { new EndpointOriginMapping("e1", "missing-origin") }
                        };

                        using (RestRequest req = new RestRequest(h.Url("/_sb/v1.0/config/validate"), HttpMethod.Post))
                        {
                            req.Authorization.BearerToken = AdminToken;
                            req.ContentType = "application/json";
                            using (RestResponse resp = await req.SendAsync(JsonSerializer.Serialize(bad)))
                            {
                                Check.Equal(200, resp.StatusCode, "validate status");
                                ConfigValidationResult result = JsonSerializer.Deserialize<ConfigValidationResult>(resp.DataAsString, _Json)!;
                                Check.False(result.Valid, "config invalid");
                                Check.Contains(resp.DataAsString, "OriginNotFound", "missing origin error code");
                            }
                        }
                    }),

                    Case("ValidatePassesForValidConfig", "POST /config/validate passes a consistent configuration", async (h, ct) =>
                    {
                        ConfigValidationRequest good = new ConfigValidationRequest
                        {
                            Origins = new List<OriginServerConfig> { new OriginServerConfig("o1") { Hostname = "localhost", Port = 1 } },
                            Endpoints = new List<ApiEndpointConfig> { new ApiEndpointConfig("e1") },
                            Routes = new List<EndpointRoute> { new EndpointRoute("e1", "GET", "/x") },
                            Mappings = new List<EndpointOriginMapping> { new EndpointOriginMapping("e1", "o1") }
                        };

                        using (RestRequest req = new RestRequest(h.Url("/_sb/v1.0/config/validate"), HttpMethod.Post))
                        {
                            req.Authorization.BearerToken = AdminToken;
                            req.ContentType = "application/json";
                            using (RestResponse resp = await req.SendAsync(JsonSerializer.Serialize(good)))
                            {
                                Check.Equal(200, resp.StatusCode, "validate status");
                                ConfigValidationResult result = JsonSerializer.Deserialize<ConfigValidationResult>(resp.DataAsString, _Json)!;
                                Check.True(result.Valid, "config valid");
                            }
                        }
                    }),

                    // ---- Health / current user / OpenAPI ----
                    Case("HealthEndpoint", "GET /health returns 200", async (h, ct) =>
                    {
                        (int s, _) = await Send(h, HttpMethod.Get, "/health");
                        Check.Equal(200, s, "health status");
                    }),

                    Case("MeEndpoint", "GET /me returns the current user for the admin token and 401 without one", async (h, ct) =>
                    {
                        (int s, string b) = await Send(h, HttpMethod.Get, "/me");
                        Check.Equal(200, s, "me status");
                        Check.Contains(b, "admin", "me identifies the admin user");
                        (int ns, _) = await Send(h, HttpMethod.Get, "/me", token: null);
                        Check.Equal(401, ns, "me without a token");
                    }),

                    Case("OpenApiDocument", "GET /openapi.json returns the OpenAPI document", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/openapi.json")))
                        using (RestResponse resp = await req.SendAsync())
                        {
                            Check.Equal(200, resp.StatusCode, "openapi status");
                            Check.Contains(resp.DataAsString, "openapi", "openapi document body");
                        }
                    }),

                    Case("SwaggerUi", "GET /swagger returns the Swagger UI", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/swagger")))
                        using (RestResponse resp = await req.SendAsync())
                            Check.Equal(200, resp.StatusCode, "swagger status");
                    }),

                    // ---- Origins CRUD ----
                    Case("OriginCrud", "Origin create, get, list, update, and delete", async (h, ct) =>
                    {
                        (int cs, string cb) = await Send(h, HttpMethod.Post, "/origins",
                            new { Identifier = "crud-origin", Name = "CRUD Origin", Hostname = "localhost", Port = 9001, Ssl = false });
                        Check.Equal(201, cs, "create origin");
                        string guid = GuidOf(cb);

                        (int gs, string gb) = await Send(h, HttpMethod.Get, "/origins/" + guid);
                        Check.Equal(200, gs, "get origin");
                        Check.Contains(gb, "crud-origin", "origin identifier present");

                        (_, string lb) = await Send(h, HttpMethod.Get, "/origins");
                        Check.Contains(lb, "crud-origin", "list contains origin");

                        (int us, _) = await Send(h, HttpMethod.Put, "/origins/" + guid,
                            new { Identifier = "crud-origin", Name = "CRUD Origin Edited", Hostname = "localhost", Port = 9002, Ssl = true });
                        Check.Equal(200, us, "update origin");
                        (_, string gb2) = await Send(h, HttpMethod.Get, "/origins/" + guid);
                        Check.Contains(gb2, "Edited", "update persisted");

                        (int ds, _) = await Send(h, HttpMethod.Delete, "/origins/" + guid);
                        Check.Equal(204, ds, "delete origin");
                        (int nf, _) = await Send(h, HttpMethod.Get, "/origins/" + guid);
                        Check.Equal(404, nf, "get after delete");
                    }),

                    // ---- Origins health ----
                    Case("ListOriginsHealthAuthorized", "GET /origins/health returns health for each origin", async (h, ct) =>
                    {
                        (int s, string b) = await Send(h, HttpMethod.Get, "/origins/health");
                        Check.Equal(200, s, "health list status");
                        Check.Contains(b, "Server 1", "origin present in health list");
                        Check.Contains(b, "IsHealthy", "health status field present");
                        Check.Contains(b, "History", "history field present");
                        // The harness waits for all origins to be healthy before running cases.
                        Check.Contains(b, "\"IsHealthy\": true", "origin reported healthy");
                    }),

                    Case("ListOriginsHealthUnauthorized", "GET /origins/health without a token returns 401", async (h, ct) =>
                    {
                        (int s, _) = await Send(h, HttpMethod.Get, "/origins/health", token: null);
                        Check.Equal(401, s, "health list unauthorized status");
                    }),

                    Case("ListOriginsHealthBadToken", "GET /origins/health with a wrong token returns 401", async (h, ct) =>
                    {
                        (int s, _) = await Send(h, HttpMethod.Get, "/origins/health", token: "not-the-admin-token");
                        Check.Equal(401, s, "health list bad token status");
                    }),

                    Case("GetOriginHealthAuthorized", "GET /origins/{guid}/health returns a single origin's health", async (h, ct) =>
                    {
                        (int ls, string lb) = await Send(h, HttpMethod.Get, "/origins");
                        Check.Equal(200, ls, "list origins for guid");
                        List<ResourceRef> origins = JsonSerializer.Deserialize<List<ResourceRef>>(lb, _Json) ?? new List<ResourceRef>();
                        Check.True(origins.Count > 0, "at least one origin listed");
                        string guid = origins[0].GUID ?? string.Empty;
                        Check.True(!string.IsNullOrEmpty(guid), "origin guid resolved");

                        (int s, string b) = await Send(h, HttpMethod.Get, "/origins/" + guid + "/health");
                        Check.Equal(200, s, "single health status");
                        Check.Contains(b, "IsHealthy", "health status field present");
                        Check.Contains(b, guid, "returned health carries the requested guid");
                    }),

                    Case("GetOriginHealthNotFound", "GET /origins/{guid}/health for an unknown GUID returns 404", async (h, ct) =>
                    {
                        (int s, _) = await Send(h, HttpMethod.Get, "/origins/" + Guid.NewGuid() + "/health");
                        Check.Equal(404, s, "unknown origin health not found");
                    }),

                    Case("GetOriginHealthBadGuid", "GET /origins/{guid}/health with a malformed GUID returns 400", async (h, ct) =>
                    {
                        (int s, _) = await Send(h, HttpMethod.Get, "/origins/not-a-guid/health");
                        Check.Equal(400, s, "malformed guid bad request");
                    }),

                    Case("GetOriginHealthUnauthorized", "GET /origins/{guid}/health without a token returns 401", async (h, ct) =>
                    {
                        (int s, _) = await Send(h, HttpMethod.Get, "/origins/" + Guid.NewGuid() + "/health", token: null);
                        Check.Equal(401, s, "single health unauthorized status");
                    }),

                    Case("OriginHealthDeduplicatesSharedTarget", "Origins sharing a health-check target are probed once and report consistent health", async (h, ct) =>
                    {
                        // Point three origins at the same live backend (same host:port:method:url) so they
                        // resolve to a single shared health monitor.
                        int livePort = h.Settings.Origins[0].Port;
                        string[] ids = new[] { "dedup-a", "dedup-b", "dedup-c" };
                        foreach (string id in ids)
                        {
                            (int cs, _) = await Send(h, HttpMethod.Post, "/origins",
                                new { Identifier = id, Name = id, Hostname = "127.0.0.1", Port = livePort, Ssl = false, HealthCheckMethod = "GET", HealthCheckUrl = "/", HealthCheckIntervalMs = 1000, HealthyThreshold = 1, UnhealthyThreshold = 2 });
                            Check.Equal(201, cs, "create " + id);
                        }

                        try
                        {
                            await WaitForHealthyAsync(h, ids, TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);

                            // Read all from a single snapshot; sharing one monitor keeps their rolling
                            // histories in lockstep (allowing at most one sample of skew from the snapshot
                            // being taken mid fan-out).
                            List<OriginServerHealthStatus> all = await FetchAllOriginHealth(h).ConfigureAwait(false);
                            OriginServerHealthStatus a = FindHealth(all, "dedup-a")!;
                            OriginServerHealthStatus b = FindHealth(all, "dedup-b")!;
                            OriginServerHealthStatus c = FindHealth(all, "dedup-c")!;

                            Check.True(a.IsHealthy && b.IsHealthy && c.IsHealthy, "all shared origins healthy");
                            Check.True(a.History.Count > 0, "origins accrued history");
                            Check.True(Math.Abs(a.History.Count - b.History.Count) <= 1 && Math.Abs(a.History.Count - c.History.Count) <= 1,
                                "shared-target histories stay consistent (a=" + a.History.Count + ", b=" + b.History.Count + ", c=" + c.History.Count + ")");

                            // The target must be probed once per interval, not once per subscriber: over a
                            // ~4s window at a 1s interval each origin should gain ~4 samples, not ~12.
                            int before = FindHealth(all, "dedup-a")!.History.Count;
                            await Task.Delay(4000, ct).ConfigureAwait(false);
                            int after = FindHealth(await FetchAllOriginHealth(h).ConfigureAwait(false), "dedup-a")!.History.Count;
                            int delta = after - before;
                            Check.True(delta >= 1 && delta <= 8,
                                "history grows ~once per interval, not once per subscriber (delta=" + delta + " over ~4s at 1s interval, 3 origins)");
                        }
                        finally
                        {
                            foreach (string id in ids) await DeleteOriginByIdentifier(h, id).ConfigureAwait(false);
                        }
                    }),

                    // ---- Endpoints CRUD ----
                    Case("EndpointCrud", "Endpoint create, get, list, update, and delete", async (h, ct) =>
                    {
                        (int cs, string cb) = await Send(h, HttpMethod.Post, "/endpoints",
                            new { Identifier = "crud-endpoint", Name = "CRUD Endpoint", LoadBalancingMode = "RoundRobin", TimeoutMs = 60000, MaxRequestBodySize = 1048576 });
                        Check.Equal(201, cs, "create endpoint");
                        string guid = GuidOf(cb);

                        (int gs, string gb) = await Send(h, HttpMethod.Get, "/endpoints/" + guid);
                        Check.Equal(200, gs, "get endpoint");
                        Check.Contains(gb, "crud-endpoint", "endpoint identifier present");

                        (_, string lb) = await Send(h, HttpMethod.Get, "/endpoints");
                        Check.Contains(lb, "crud-endpoint", "list contains endpoint");

                        (int us, _) = await Send(h, HttpMethod.Put, "/endpoints/" + guid,
                            new { Identifier = "crud-endpoint", Name = "CRUD Endpoint Edited", LoadBalancingMode = "Random", TimeoutMs = 30000, MaxRequestBodySize = 1048576 });
                        Check.Equal(200, us, "update endpoint");

                        (int ds, _) = await Send(h, HttpMethod.Delete, "/endpoints/" + guid);
                        Check.Equal(204, ds, "delete endpoint");
                        (int nf, _) = await Send(h, HttpMethod.Get, "/endpoints/" + guid);
                        Check.Equal(404, nf, "get after delete");
                    }),

                    // ---- Routes CRUD ----
                    Case("RouteCrud", "Route create, get, update, and delete", async (h, ct) =>
                    {
                        (_, string eb) = await Send(h, HttpMethod.Post, "/endpoints",
                            new { Identifier = "route-crud-ep", Name = "Route CRUD", LoadBalancingMode = "RoundRobin" });
                        string eguid = GuidOf(eb);

                        (int cs, _) = await Send(h, HttpMethod.Post, "/routes",
                            new { EndpointIdentifier = "route-crud-ep", EndpointGUID = eguid, HttpMethod = "GET", UrlPattern = "/rc/{id}", RequiresAuthentication = false });
                        Check.Equal(201, cs, "create route");
                        int rid = await FindId<EndpointRoute>(h, "/routes", x => x.UrlPattern, x => x.Id, "/rc/{id}");

                        (int gs, _) = await Send(h, HttpMethod.Get, "/routes/" + rid);
                        Check.Equal(200, gs, "get route");
                        (int us, _) = await Send(h, HttpMethod.Put, "/routes/" + rid,
                            new { EndpointIdentifier = "route-crud-ep", EndpointGUID = eguid, HttpMethod = "POST", UrlPattern = "/rc/{id}", RequiresAuthentication = true, SortOrder = 0 });
                        Check.Equal(200, us, "update route");
                        (int ds, _) = await Send(h, HttpMethod.Delete, "/routes/" + rid);
                        Check.Equal(204, ds, "delete route");

                        await Send(h, HttpMethod.Delete, "/endpoints/" + eguid);
                    }),

                    // ---- Mappings CRUD ----
                    Case("MappingCrud", "Mapping create, get, and delete", async (h, ct) =>
                    {
                        (_, string eb) = await Send(h, HttpMethod.Post, "/endpoints",
                            new { Identifier = "map-crud-ep", Name = "Map EP", LoadBalancingMode = "RoundRobin" });
                        string eguid = GuidOf(eb);
                        (_, string ob) = await Send(h, HttpMethod.Post, "/origins",
                            new { Identifier = "map-crud-origin", Hostname = "localhost", Port = 9003 });
                        string oguid = GuidOf(ob);

                        (int cs, _) = await Send(h, HttpMethod.Post, "/mappings",
                            new { EndpointIdentifier = "map-crud-ep", EndpointGUID = eguid, OriginIdentifier = "map-crud-origin", OriginGUID = oguid });
                        Check.Equal(201, cs, "create mapping");
                        int mid = await FindId<EndpointOriginMapping>(h, "/mappings", x => x.OriginIdentifier, x => x.Id, "map-crud-origin");

                        (int gs, _) = await Send(h, HttpMethod.Get, "/mappings/" + mid);
                        Check.Equal(200, gs, "get mapping");
                        (int ds, _) = await Send(h, HttpMethod.Delete, "/mappings/" + mid);
                        Check.Equal(204, ds, "delete mapping");

                        await Send(h, HttpMethod.Delete, "/endpoints/" + eguid);
                        await Send(h, HttpMethod.Delete, "/origins/" + oguid);
                    }),

                    // ---- Rewrites CRUD (any-method create carries no GUID) ----
                    Case("RewriteCrud", "Rewrite create (any method), get, update, and delete", async (h, ct) =>
                    {
                        (_, string eb) = await Send(h, HttpMethod.Post, "/endpoints",
                            new { Identifier = "rw-crud-ep", Name = "RW EP", LoadBalancingMode = "RoundRobin" });
                        string eguid = GuidOf(eb);

                        (int cs, _) = await Send(h, HttpMethod.Post, "/rewrites",
                            new { EndpointIdentifier = "rw-crud-ep", HttpMethod = "", SourcePattern = "/old", TargetPattern = "/new", SortOrder = 0 });
                        Check.Equal(201, cs, "create rewrite with empty (any) method");
                        int rwid = await FindId<UrlRewrite>(h, "/rewrites", x => x.SourcePattern, x => x.Id, "/old");

                        (int gs, _) = await Send(h, HttpMethod.Get, "/rewrites/" + rwid);
                        Check.Equal(200, gs, "get rewrite");
                        (int us, _) = await Send(h, HttpMethod.Put, "/rewrites/" + rwid,
                            new { EndpointIdentifier = "rw-crud-ep", HttpMethod = "GET", SourcePattern = "/old", TargetPattern = "/newer", SortOrder = 1 });
                        Check.Equal(200, us, "update rewrite");
                        (int ds, _) = await Send(h, HttpMethod.Delete, "/rewrites/" + rwid);
                        Check.Equal(204, ds, "delete rewrite");

                        await Send(h, HttpMethod.Delete, "/endpoints/" + eguid);
                    }),

                    // ---- Blocked headers CRUD ----
                    Case("BlockedHeaderCrud", "Blocked header create, get, and delete", async (h, ct) =>
                    {
                        (int cs, _) = await Send(h, HttpMethod.Post, "/headers", new { HeaderName = "X-Crud-Test" });
                        Check.Equal(201, cs, "create header");
                        int hid = await FindId<BlockedHeader>(h, "/headers", x => x.HeaderName, x => x.Id, "x-crud-test", ignoreCase: true);

                        (int gs, _) = await Send(h, HttpMethod.Get, "/headers/" + hid);
                        Check.Equal(200, gs, "get header");
                        (int ds, _) = await Send(h, HttpMethod.Delete, "/headers/" + hid);
                        Check.Equal(204, ds, "delete header");
                    }),

                    // ---- Users CRUD ----
                    Case("UserCrud", "User create, get, list, update, and delete", async (h, ct) =>
                    {
                        (int cs, string cb) = await Send(h, HttpMethod.Post, "/users",
                            new { Username = "cruduser", Email = "crud@example.com", FirstName = "Crud", LastName = "User", Active = true, IsAdmin = false });
                        Check.Equal(201, cs, "create user");
                        string guid = GuidOf(cb);

                        (int gs, string gb) = await Send(h, HttpMethod.Get, "/users/" + guid);
                        Check.Equal(200, gs, "get user");
                        Check.Contains(gb, "cruduser", "username present");

                        (_, string lb) = await Send(h, HttpMethod.Get, "/users");
                        Check.Contains(lb, "cruduser", "list contains user");

                        (int us, _) = await Send(h, HttpMethod.Put, "/users/" + guid,
                            new { Username = "cruduser", Email = "crud2@example.com", FirstName = "Crud", LastName = "Edited", Active = true, IsAdmin = false });
                        Check.Equal(200, us, "update user");

                        (int ds, _) = await Send(h, HttpMethod.Delete, "/users/" + guid);
                        Check.Equal(204, ds, "delete user");
                        (int nf, _) = await Send(h, HttpMethod.Get, "/users/" + guid);
                        Check.Equal(404, nf, "get after delete");
                    }),

                    // ---- Credentials CRUD + regenerate ----
                    Case("CredentialCrud", "Credential create, get, list, update, regenerate, and delete", async (h, ct) =>
                    {
                        (_, string ub) = await Send(h, HttpMethod.Post, "/users",
                            new { Username = "creduser", Email = "c@example.com", FirstName = "Cred", LastName = "User", Active = true, IsAdmin = false });
                        string uguid = GuidOf(ub);

                        (int cs, string cb) = await Send(h, HttpMethod.Post, "/credentials",
                            new { UserGUID = uguid, Name = "Crud Cred", Active = true, IsReadOnly = false });
                        Check.Equal(201, cs, "create credential");
                        string cguid = GuidOf(cb);

                        (int gs, _) = await Send(h, HttpMethod.Get, "/credentials/" + cguid);
                        Check.Equal(200, gs, "get credential");
                        (_, string lb) = await Send(h, HttpMethod.Get, "/credentials");
                        Check.Contains(lb, "Crud Cred", "list contains credential");

                        (int us, _) = await Send(h, HttpMethod.Put, "/credentials/" + cguid,
                            new { UserGUID = uguid, Name = "Crud Cred Edited", Active = true, IsReadOnly = true });
                        Check.Equal(200, us, "update credential");
                        (int rs, _) = await Send(h, HttpMethod.Post, "/credentials/" + cguid + "/regenerate");
                        Check.Equal(200, rs, "regenerate credential");
                        (int ds, _) = await Send(h, HttpMethod.Delete, "/credentials/" + cguid);
                        Check.Equal(204, ds, "delete credential");

                        await Send(h, HttpMethod.Delete, "/users/" + uguid);
                    }),

                    // ---- Request history endpoints ----
                    Case("HistoryEndpoints", "Request history list, recent, failed, stats, get-by-id, delete, and cleanup", async (h, ct) =>
                    {
                        RequestHistory ok = await InsertHistoryRowReturning(h, true, 12, ct);
                        RequestHistory bad = await InsertHistoryRowReturning(h, false, 34, ct);

                        (int ls, _) = await Send(h, HttpMethod.Get, "/history"); Check.Equal(200, ls, "history list");
                        (int rs, _) = await Send(h, HttpMethod.Get, "/history/recent?count=10"); Check.Equal(200, rs, "history recent");
                        (int fs, _) = await Send(h, HttpMethod.Get, "/history/failed"); Check.Equal(200, fs, "history failed");
                        (int ss, _) = await Send(h, HttpMethod.Get, "/history/stats"); Check.Equal(200, ss, "history stats");

                        (int gs, _) = await Send(h, HttpMethod.Get, "/history/" + ok.RequestId); Check.Equal(200, gs, "history get by request id");
                        (int ds, _) = await Send(h, HttpMethod.Delete, "/history/" + bad.RequestId); Check.Equal(204, ds, "history delete by request id");
                        (int nf, _) = await Send(h, HttpMethod.Get, "/history/" + bad.RequestId); Check.Equal(404, nf, "history get after delete");
                        (int cs, _) = await Send(h, HttpMethod.Post, "/history/cleanup?days=30"); Check.True(cs == 200 || cs == 204, "history cleanup");
                    }),

                    // ---- Authentication / authorization on writes ----
                    Case("WriteRequiresAuth", "A write without a token returns 401", async (h, ct) =>
                    {
                        (int s, _) = await Send(h, HttpMethod.Post, "/origins",
                            new { Identifier = "noauth-origin", Hostname = "localhost", Port = 9004 }, token: null);
                        Check.Equal(401, s, "write without token is unauthorized");
                    }),

                    Case("ReadOnlyCredentialForbidden", "A read-only credential can read but is forbidden (403) from writing", async (h, ct) =>
                    {
                        (_, string ub) = await Send(h, HttpMethod.Post, "/users",
                            new { Username = "rouser", Email = "ro@example.com", FirstName = "RO", LastName = "User", Active = true, IsAdmin = false });
                        string uguid = GuidOf(ub);
                        (int cs, string cb) = await Send(h, HttpMethod.Post, "/credentials",
                            new { UserGUID = uguid, Name = "RO Cred", Active = true, IsReadOnly = true });
                        Check.Equal(201, cs, "create read-only credential");
                        string roToken = JsonSerializer.Deserialize<Credential>(cb, _Json)?.BearerToken ?? string.Empty;
                        Check.True(!string.IsNullOrEmpty(roToken), "credential returns a bearer token");
                        string cguid = GuidOf(cb);

                        (int rs, _) = await Send(h, HttpMethod.Get, "/origins", token: roToken);
                        Check.Equal(200, rs, "read-only credential can read");
                        (int ws, _) = await Send(h, HttpMethod.Post, "/origins",
                            new { Identifier = "ro-denied", Hostname = "localhost", Port = 9005 }, token: roToken);
                        Check.Equal(403, ws, "read-only credential write is forbidden (403, not 401)");

                        await Send(h, HttpMethod.Delete, "/credentials/" + cguid);
                        await Send(h, HttpMethod.Delete, "/users/" + uguid);
                    })
                });
        }

        private static async Task InsertHistoryRow(ProxyHarness h, bool success, long durationMs, System.Threading.CancellationToken ct)
        {
            // Note: the store stamps TimestampUtc at insert time; callers cannot backdate rows.
            RequestHistory row = new RequestHistory
            {
                HttpMethod = "GET",
                RequestPath = "/timeseries-test",
                StatusCode = success ? 200 : 500,
                Success = success,
                DurationMs = durationMs
            };

            await h.Daemon!.Client.RequestHistory.CreateAsync(row, ct).ConfigureAwait(false);
        }

        private static async Task<RequestHistory> InsertHistoryRowReturning(ProxyHarness h, bool success, long durationMs, System.Threading.CancellationToken ct)
        {
            RequestHistory row = new RequestHistory
            {
                HttpMethod = "GET",
                RequestPath = "/history-crud-test",
                StatusCode = success ? 200 : 500,
                Success = success,
                DurationMs = durationMs
            };

            return await h.Daemon!.Client.RequestHistory.CreateAsync(row, ct).ConfigureAwait(false);
        }

        // Issue a management API request (path relative to /_sb/v1.0). A null token omits the
        // Authorization header; a non-null body is JSON-serialized. Returns status and body text.
        private static async Task<(int Status, string Body)> Send(
            ProxyHarness h, HttpMethod method, string path, object? body = null, string? token = AdminToken)
        {
            using (RestRequest req = new RestRequest(h.Url("/_sb/v1.0" + path), method))
            {
                if (token != null) req.Authorization.BearerToken = token;

                string? payload = null;
                if (body != null)
                {
                    req.ContentType = "application/json";
                    payload = JsonSerializer.Serialize(body);
                }

                using (RestResponse resp = payload != null ? await req.SendAsync(payload) : await req.SendAsync())
                    return (resp.StatusCode, resp.DataAsString ?? string.Empty);
            }
        }

        private static string GuidOf(string json) => JsonSerializer.Deserialize<ResourceRef>(json, _Json)?.GUID ?? string.Empty;

        // Fetch the full origin-health list in a single snapshot.
        private static async Task<List<OriginServerHealthStatus>> FetchAllOriginHealth(ProxyHarness h)
        {
            (int status, string body) = await Send(h, HttpMethod.Get, "/origins/health");
            if (status != 200) return new List<OriginServerHealthStatus>();
            return JsonSerializer.Deserialize<List<OriginServerHealthStatus>>(body, _Json) ?? new List<OriginServerHealthStatus>();
        }

        private static OriginServerHealthStatus? FindHealth(List<OriginServerHealthStatus> list, string identifier)
        {
            foreach (OriginServerHealthStatus status in list)
                if (status.Identifier == identifier) return status;
            return null;
        }

        // Poll the health endpoint until every named origin reports healthy, or throw on timeout.
        private static async Task WaitForHealthyAsync(ProxyHarness h, string[] identifiers, TimeSpan timeout, System.Threading.CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (true)
            {
                List<OriginServerHealthStatus> all = await FetchAllOriginHealth(h);
                bool allHealthy = true;
                foreach (string identifier in identifiers)
                {
                    OriginServerHealthStatus? status = FindHealth(all, identifier);
                    if (status == null || !status.IsHealthy) { allHealthy = false; break; }
                }
                if (allHealthy) return;
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("origins did not all become healthy within " + timeout.TotalSeconds + "s");
                await Task.Delay(500, token);
            }
        }

        // Resolve an origin's GUID by identifier from the origins list and delete it (best-effort cleanup).
        private static async Task DeleteOriginByIdentifier(ProxyHarness h, string identifier)
        {
            (int status, string body) = await Send(h, HttpMethod.Get, "/origins");
            if (status != 200) return;
            List<OriginServerConfig> configs = JsonSerializer.Deserialize<List<OriginServerConfig>>(body, _Json) ?? new List<OriginServerConfig>();
            foreach (OriginServerConfig config in configs)
            {
                if (config.Identifier == identifier)
                {
                    await Send(h, HttpMethod.Delete, "/origins/" + config.GUID);
                    return;
                }
            }
        }

        // Locate the auto-increment Id of a listed resource by matching a field, since create responses
        // do not carry the generated Id. Strongly typed over the resource model T with selectors for the
        // field to match and the Id to return.
        private static async Task<int> FindId<T>(
            ProxyHarness h, string listPath, Func<T, string?> field, Func<T, int> id, string value, bool ignoreCase = false)
        {
            (int status, string bodyText) = await Send(h, HttpMethod.Get, listPath);
            Check.Equal(200, status, "list for id lookup: " + listPath);

            List<T> items = JsonSerializer.Deserialize<List<T>>(bodyText, _Json) ?? new List<T>();
            foreach (T item in items)
            {
                string? v = field(item);
                if (v != null && (ignoreCase ? string.Equals(v, value, StringComparison.OrdinalIgnoreCase) : v == value))
                    return id(item);
            }
            throw new Exception("could not locate '" + value + "' in " + listPath);
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, System.Func<ProxyHarness, System.Threading.CancellationToken, Task> body)
        {
            return new TestCaseDescriptor(
                suiteId: "Management",
                caseId: caseId,
                displayName: displayName,
                executeAsync: async ct =>
                {
                    await TestHarnesses.Management.StartAsync(ct).ConfigureAwait(false);
                    await body(TestHarnesses.Management, ct).ConfigureAwait(false);
                });
        }

        // ---- Named response shapes for deserialization (no System.Text.Json DOM types) ----

        // Wrapper for GET /history/timeseries. Buckets reuse the real TimeSeriesBucket model; the
        // camelCase JSON binds to its PascalCase properties via case-insensitive matching.
        private sealed class TimeSeriesResponse
        {
            public List<TimeSeriesBucket> Buckets { get; set; } = new List<TimeSeriesBucket>();
        }

        // Subset of the POST /config/validate response needed by the assertions.
        private sealed class ConfigValidationResult
        {
            public bool Valid { get; set; }
        }

        // Minimal projection to read a resource's GUID (and Id) from a create/list response.
        private sealed class ResourceRef
        {
            public string GUID { get; set; } = string.Empty;
            public int Id { get; set; }
        }
    }
}
