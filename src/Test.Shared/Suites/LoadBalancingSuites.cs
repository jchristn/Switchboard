namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using RestWrapper;
    using Switchboard.Core;
    using Switchboard.Core.Models;
    using Switchboard.Core.Settings;
    using Test.Shared.Harness;
    using Touchstone.Core;

    /// <summary>
    /// Integration suite for load balancing and resilient routing. Each case boots a real
    /// <see cref="SwitchboardDaemon"/> in front of live <see cref="OriginHost"/> backends on random
    /// ports, configures the endpoint's routing behavior, drives real HTTP traffic through the proxy,
    /// and asserts which origin served each request (via the <c>X-Origin-Server</c> response header) and
    /// the resulting status codes. Positive and negative cases are included per capability.
    /// </summary>
    public static class LoadBalancingSuites
    {
        /// <summary>
        /// All load-balancing integration suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the load-balancing suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "LoadBalancing",
                displayName: "Load Balancing and Resilient Routing",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("LoadBalancing", "WeightedDistribution", "Weighted mode splits traffic by per-mapping weight",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: settings =>
                            {
                                ApiEndpoint ep = settings.Endpoints[0];
                                ep.LoadBalancing = LoadBalancingMode.Weighted;
                                ep.OriginBindings = new List<OriginBinding>
                                {
                                    new OriginBinding(settings.Origins[0].Identifier) { Weight = 1 },
                                    new OriginBinding(settings.Origins[1].Identifier) { Weight = 4 }
                                };
                            });
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                Dictionary<string, int> counts = await CountAsync(harness, 250, null, null).ConfigureAwait(false);
                                string heavy = settings2(harness, 1);
                                string light = settings2(harness, 0);
                                Check.True(counts.TryGetValue(heavy, out int h) && h > 0, "heavy origin served traffic");
                                Check.True(counts.TryGetValue(light, out int l) && l > 0, "light origin served some traffic");
                                Check.True(h > l * 1.8, "weight-4 origin served far more than weight-1 (heavy=" + h + ", light=" + l + ")");
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("LoadBalancing", "WeightZeroDrains", "A weight-0 mapping never receives traffic",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: settings =>
                            {
                                ApiEndpoint ep = settings.Endpoints[0];
                                ep.LoadBalancing = LoadBalancingMode.RoundRobin;
                                ep.OriginBindings = new List<OriginBinding>
                                {
                                    new OriginBinding(settings.Origins[0].Identifier) { Weight = 0 },
                                    new OriginBinding(settings.Origins[1].Identifier) { Weight = 100 }
                                };
                            });
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                Dictionary<string, int> counts = await CountAsync(harness, 40, null, null).ConfigureAwait(false);
                                Check.Equal(0, counts.TryGetValue(settings2(harness, 0), out int drained) ? drained : 0, "drained origin got no traffic");
                                Check.Equal(40, counts.TryGetValue(settings2(harness, 1), out int live) ? live : 0, "all traffic to the live origin");
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("LoadBalancing", "RetryFailsOverToHealthyOrigin", "A retryable 5xx fails over to another origin and the client sees success",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: settings =>
                            {
                                ApiEndpoint ep = settings.Endpoints[0];
                                ep.LoadBalancing = LoadBalancingMode.RoundRobin;
                                ep.MaxRetries = 1;
                                ep.RetryOn5xx = true;
                                // Disable passive ejection so this case isolates retry behavior.
                                foreach (OriginServer o in settings.Origins) o.MaxFailures = 0;
                            });
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                harness.Origins[0].ForcedStatusCode = 500; // real traffic fails, active health stays green
                                for (int i = 0; i < 20; i++)
                                {
                                    RequestOutcome outcome = await SendAsync(harness, "/unauthenticated", null, null).ConfigureAwait(false);
                                    Check.Equal(200, outcome.Status, "request " + i + " succeeded via failover");
                                }
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("LoadBalancing", "RetryDisabledSurfacesFailure", "With retries disabled, an origin's 5xx reaches the client",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: settings =>
                            {
                                ApiEndpoint ep = settings.Endpoints[0];
                                ep.LoadBalancing = LoadBalancingMode.RoundRobin;
                                ep.MaxRetries = 0;
                                foreach (OriginServer o in settings.Origins) o.MaxFailures = 0; // no ejection either
                            });
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                harness.Origins[0].ForcedStatusCode = 500;
                                int failures = 0;
                                for (int i = 0; i < 20; i++)
                                {
                                    RequestOutcome outcome = await SendAsync(harness, "/unauthenticated", null, null).ConfigureAwait(false);
                                    if (outcome.Status == 500) failures++;
                                }
                                Check.True(failures > 0, "some requests surfaced the origin 500 (got " + failures + ")");
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("LoadBalancing", "PassiveEjectionRemovesFailingOrigin", "An origin returning 5xx is passively ejected and stops receiving traffic",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: settings =>
                            {
                                ApiEndpoint ep = settings.Endpoints[0];
                                ep.LoadBalancing = LoadBalancingMode.RoundRobin;
                                ep.MaxRetries = 0;
                                settings.Origins[0].MaxFailures = 3;
                                settings.Origins[0].EjectionDurationMs = 60000;
                            });
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                harness.Origins[0].ForcedStatusCode = 500;
                                List<RequestOutcome> outcomes = new List<RequestOutcome>();
                                for (int i = 0; i < 30; i++)
                                    outcomes.Add(await SendAsync(harness, "/unauthenticated", null, null).ConfigureAwait(false));

                                Check.True(outcomes.Take(10).Any(o => o.Status == 500), "the failing origin served (and 500'd) before ejection");
                                Check.True(outcomes.Skip(20).All(o => o.Status == 200), "after ejection every request succeeds via the healthy origin");
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("LoadBalancing", "PassiveEjectionDisabledKeepsFailing", "With ejection disabled the failing origin keeps receiving traffic",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: settings =>
                            {
                                ApiEndpoint ep = settings.Endpoints[0];
                                ep.LoadBalancing = LoadBalancingMode.RoundRobin;
                                ep.MaxRetries = 0;
                                foreach (OriginServer o in settings.Origins) o.MaxFailures = 0;
                            });
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                harness.Origins[0].ForcedStatusCode = 500;
                                List<RequestOutcome> outcomes = new List<RequestOutcome>();
                                for (int i = 0; i < 20; i++)
                                    outcomes.Add(await SendAsync(harness, "/unauthenticated", null, null).ConfigureAwait(false));
                                Check.True(outcomes.Skip(10).Any(o => o.Status == 500), "failing origin never ejected, still 500'ing late in the run");
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("LoadBalancing", "StickySessionPinsByHeader", "Sticky sessions route a header value to a single origin",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(3), configure: settings =>
                            {
                                ApiEndpoint ep = settings.Endpoints[0];
                                ep.LoadBalancing = LoadBalancingMode.RoundRobin;
                                ep.StickySessionEnabled = true;
                                ep.StickySessionHeader = "X-Session";
                            });
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                Dictionary<string, int> counts = await CountAsync(harness, 24, "X-Session", "alpha").ConfigureAwait(false);
                                Check.Equal(1, counts.Count, "one session key pinned to exactly one origin");
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("LoadBalancing", "StickyDisabledDistributes", "Without sticky sessions the same header distributes across origins",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(3), configure: settings =>
                            {
                                settings.Endpoints[0].LoadBalancing = LoadBalancingMode.RoundRobin;
                            });
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                Dictionary<string, int> counts = await CountAsync(harness, 24, "X-Session", "alpha").ConfigureAwait(false);
                                Check.True(counts.Count > 1, "traffic distributed across multiple origins (got " + counts.Count + ")");
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("LoadBalancing", "PriorityTierFailover", "Backup-tier origins receive traffic only when the primary tier is down",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: settings =>
                            {
                                ApiEndpoint ep = settings.Endpoints[0];
                                ep.LoadBalancing = LoadBalancingMode.RoundRobin;
                                ep.OriginBindings = new List<OriginBinding>
                                {
                                    new OriginBinding(settings.Origins[0].Identifier) { Priority = 0 },
                                    new OriginBinding(settings.Origins[1].Identifier) { Priority = 1 }
                                };
                            });
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                string primary = settings2(harness, 0);
                                string backup = settings2(harness, 1);

                                Dictionary<string, int> healthy = await CountAsync(harness, 10, null, null).ConfigureAwait(false);
                                Check.Equal(10, healthy.TryGetValue(primary, out int p) ? p : 0, "primary tier serves all while healthy");
                                Check.Equal(0, healthy.TryGetValue(backup, out int b) ? b : 0, "backup tier idle while primary healthy");

                                harness.Origins[0].Stop();
                                await WaitAsync(() => !harness.Settings.Origins[0].Healthy, TimeSpan.FromSeconds(25), ct).ConfigureAwait(false);

                                Dictionary<string, int> failed = await CountAsync(harness, 10, null, null).ConfigureAwait(false);
                                Check.Equal(10, failed.TryGetValue(backup, out int b2) ? b2 : 0, "backup tier serves when primary is down");
                            }
                            finally { harness.Dispose(); }
                        })
                });
        }

        #region Helpers

        private static IReadOnlyList<string> Origins(int count)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < count; i++)
                names.Add("Server " + (i + 1));
            return names;
        }

        // Identifier of the Nth configured origin (also its X-Origin-Server value).
        private static string settings2(ProxyHarness harness, int index)
        {
            return harness.Settings.Origins[index].Identifier;
        }

        private static async Task<RequestOutcome> SendAsync(ProxyHarness harness, string path, string? headerName, string? headerValue)
        {
            using (RestRequest req = new RestRequest(harness.Url(path)))
            {
                if (headerName != null) req.Headers.Add(headerName, headerValue);
                using (RestResponse resp = await req.SendAsync().ConfigureAwait(false))
                {
                    RequestOutcome outcome = new RequestOutcome();
                    outcome.Status = resp.StatusCode;
                    outcome.OriginName = resp.Headers != null ? resp.Headers.Get("X-Origin-Server") : null;
                    return outcome;
                }
            }
        }

        private static async Task<Dictionary<string, int>> CountAsync(ProxyHarness harness, int requests, string? headerName, string? headerValue)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < requests; i++)
            {
                RequestOutcome outcome = await SendAsync(harness, "/unauthenticated", headerName, headerValue).ConfigureAwait(false);
                if (outcome.Status == 200 && !String.IsNullOrEmpty(outcome.OriginName))
                    counts[outcome.OriginName] = (counts.TryGetValue(outcome.OriginName, out int c) ? c : 0) + 1;
            }
            return counts;
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

        private sealed class RequestOutcome
        {
            public int Status { get; set; }
            public string? OriginName { get; set; }
        }

        #endregion
    }
}
