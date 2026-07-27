namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
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

                                JsonNode root = JsonNode.Parse(resp.DataAsString)!;
                                JsonArray buckets = root["buckets"]!.AsArray();
                                Check.True(buckets.Count >= 1, "bucket count");

                                // All inserted rows fall in the single hour-wide bucket.
                                long total = 0, success = 0, failure = 0;
                                double weightedDuration = 0;
                                foreach (JsonNode? b in buckets)
                                {
                                    long t = b!["Total"]!.GetValue<long>();
                                    total += t;
                                    success += b["Success"]!.GetValue<long>();
                                    failure += b["Failure"]!.GetValue<long>();
                                    weightedDuration += b["AvgDurationMs"]!.GetValue<double>() * t;
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

                                JsonNode root = JsonNode.Parse(resp.DataAsString)!;
                                Check.Equal("********", root["Management"]!["AdminToken"]!.GetValue<string>(), "admin token masked");
                                Check.False(resp.DataAsString.Contains(AdminToken), "raw admin token not present");
                                Check.True(root["restartRequiredSettings"]!.AsArray().Count > 0, "restart list present");
                                Check.True(root["runtimeEditableSettings"]!.AsArray().Count > 0, "runtime list present");
                            }
                        }
                    }),

                    Case("SettingsPutRoundTrip", "PUT /settings applies a hot-swappable field live and preserves masked secrets", async (h, ct) =>
                    {
                        JsonObject settings;
                        using (RestRequest getReq = new RestRequest(h.Url("/_sb/v1.0/settings")))
                        {
                            getReq.Authorization.BearerToken = AdminToken;
                            using (RestResponse getResp = await getReq.SendAsync())
                                settings = JsonNode.Parse(getResp.DataAsString)!.AsObject();
                        }

                        // Mutate a runtime-editable field and round-trip the masked admin token unchanged.
                        settings["Logging"]!["MinimumSeverity"] = 4;

                        using (RestRequest putReq = new RestRequest(h.Url("/_sb/v1.0/settings"), HttpMethod.Put))
                        {
                            putReq.Authorization.BearerToken = AdminToken;
                            putReq.ContentType = "application/json";
                            using (RestResponse putResp = await putReq.SendAsync(settings.ToJsonString()))
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
                                JsonNode root = JsonNode.Parse(verifyResp.DataAsString)!;
                                Check.Equal(4, root["Logging"]!["MinimumSeverity"]!.GetValue<int>(), "severity applied live");
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
                                JsonNode root = JsonNode.Parse(resp.DataAsString)!;
                                Check.False(root["valid"]!.GetValue<bool>(), "config invalid");
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
                                JsonNode root = JsonNode.Parse(resp.DataAsString)!;
                                Check.True(root["valid"]!.GetValue<bool>(), "config valid");
                            }
                        }
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
    }
}
