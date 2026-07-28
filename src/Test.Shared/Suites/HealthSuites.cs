namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using RestWrapper;
    using Test.Shared.Harness;
    using Touchstone.Core;

    /// <summary>
    /// Integration suite for health checking, gateway error behavior, and rate limiting. Every case
    /// owns a dedicated harness on isolated ports and tears it down when finished, so these
    /// destructive scenarios never disturb the shared read-only harness and remain order-independent.
    /// </summary>
    public static class HealthSuites
    {
        /// <summary>
        /// All health/resilience integration suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the health and rate-limit suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Health",
                displayName: "Health, Gateway Errors, and Rate Limiting",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Health", "AllOriginsDownReturns502", "All origins down yields 502 Bad Gateway; recovery restores 200",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(4));
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);

                                foreach (OriginHost origin in harness.Origins) origin.Stop();
                                await WaitAsync(() => harness.Settings.Origins.All(o => !o.Healthy), TimeSpan.FromSeconds(25), ct).ConfigureAwait(false);

                                int badGatewayCount = 0;
                                for (int i = 0; i < 3; i++)
                                {
                                    using (RestRequest req = new RestRequest(harness.Url("/unauthenticated")))
                                    using (RestResponse resp = await req.SendAsync())
                                        if (resp.StatusCode == 502) badGatewayCount++;
                                }
                                Check.True(badGatewayCount > 0, "at least one 502 when all origins down");

                                foreach (OriginHost origin in harness.Origins) origin.Start();
                                await harness.WaitForAllHealthyAsync(TimeSpan.FromSeconds(25), ct).ConfigureAwait(false);

                                using (RestRequest req = new RestRequest(harness.Url("/unauthenticated")))
                                using (RestResponse resp = await req.SendAsync())
                                    Check.Equal(200, resp.StatusCode, "recovered request succeeds");
                            }
                            finally
                            {
                                harness.Dispose();
                            }
                        }),

                    new TestCaseDescriptor("Health", "RateLimiting", "Burst traffic against a constrained origin only ever returns 200 or 429",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(1), maxParallelRequests: 2, rateLimitThreshold: 3);
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);

                                Task<int>[] tasks = new Task<int>[20];
                                for (int i = 0; i < tasks.Length; i++)
                                {
                                    int index = i;
                                    tasks[i] = Task.Run(async () =>
                                    {
                                        if (index > 0) await Task.Delay(5).ConfigureAwait(false);
                                        using (RestRequest req = new RestRequest(harness.Url("/unauthenticated")))
                                        using (RestResponse resp = await req.SendAsync())
                                            return resp.StatusCode;
                                    });
                                }

                                int[] statuses = await Task.WhenAll(tasks).ConfigureAwait(false);
                                foreach (int status in statuses)
                                    Check.True(status == 200 || status == 429, "status is 200 or 429 (got " + status + ")");
                                Check.True(statuses.Any(s => s == 200), "at least one request succeeded");
                            }
                            finally
                            {
                                harness.Dispose();
                            }
                        }),

                    new TestCaseDescriptor("Health", "FailureDuringActiveRequests", "Load balancing survives partial origin failure mid-flight",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(4));
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);

                                Task<(int ok, int fail)> traffic = Task.Run(async () =>
                                {
                                    int ok = 0;
                                    int fail = 0;
                                    for (int i = 0; i < 10; i++)
                                    {
                                        try
                                        {
                                            using (RestRequest req = new RestRequest(harness.Url("/unauthenticated")))
                                            using (RestResponse resp = await req.SendAsync())
                                            {
                                                if (resp.StatusCode == 200) ok++;
                                                else fail++;
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            fail++;
                                        }
                                        await Task.Delay(300).ConfigureAwait(false);
                                    }
                                    return (ok, fail);
                                });

                                await Task.Delay(1000, ct).ConfigureAwait(false);
                                harness.Origins[2].Stop();
                                harness.Origins[3].Stop();

                                (int ok, int fail) result = await traffic.ConfigureAwait(false);
                                Check.True(result.fail != 10, "not all requests failed during partial outage (ok=" + result.ok + ", fail=" + result.fail + ")");
                            }
                            finally
                            {
                                harness.Dispose();
                            }
                        })
                });
        }

        private static IReadOnlyList<string> Origins(int count)
        {
            List<string> origins = new List<string>();
            for (int i = 0; i < count; i++)
                origins.Add("Server " + (i + 1));
            return origins;
        }

        private static async Task WaitAsync(Func<bool> condition, TimeSpan timeout, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Condition not met within " + timeout.TotalSeconds + "s.");
                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }
    }
}
