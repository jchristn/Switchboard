namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using RestWrapper;
    using Test.Shared.Harness;
    using Touchstone.Core;

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
                    })
                });
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
