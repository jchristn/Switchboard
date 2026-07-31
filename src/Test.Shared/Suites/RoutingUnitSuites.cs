namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Threading.Tasks;

    using Switchboard.Core;
    using Switchboard.Core.Models;
    using Switchboard.Core.Services;
    using Touchstone.Core;

    /// <summary>
    /// Network-free unit tests for the load-balancing selection logic in <see cref="OriginSelector"/>.
    /// Each case builds origins and an endpoint in memory, drives selection with a seeded random source
    /// and a fixed clock, and asserts the chosen origin(s). Positive and negative assertions are included
    /// for every routing capability.
    /// </summary>
    public static class RoutingUnitSuites
    {
        private static readonly DateTime Now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// All routing unit suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the routing unit suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "RoutingUnit",
                displayName: "Load Balancing (unit)",
                cases: new List<TestCaseDescriptor>
                {
                    Case("WeightedDistribution", "Weighted mode splits traffic in proportion to weight", () =>
                    {
                        List<OriginServer> origins = new List<OriginServer> { Origin("a"), Origin("b") };
                        ApiEndpoint ep = Ep(LoadBalancingMode.Weighted, Bind("a", 1), Bind("b", 3));
                        Dictionary<string, int> counts = Distribute(ep, origins, 8000, 7);
                        double ratio = counts["b"] / (double)counts["a"];
                        Check.True(ratio > 2.5 && ratio < 3.5, "b:a ratio near 3 (got " + ratio.ToString("F2") + ")");
                    }),

                    Case("WeightZeroDrains", "A weight-0 origin is never selected", () =>
                    {
                        List<OriginServer> origins = new List<OriginServer> { Origin("a"), Origin("b") };
                        ApiEndpoint ep = Ep(LoadBalancingMode.Weighted, Bind("a", 0), Bind("b", 100));
                        Dictionary<string, int> counts = Distribute(ep, origins, 500, 3);
                        Check.Equal(0, counts.TryGetValue("a", out int ca) ? ca : 0, "drained origin gets no traffic");
                        Check.Equal(500, counts["b"], "all traffic to the non-drained origin");
                    }),

                    Case("LeastConnectionsPicksIdle", "Least-connections selects the origin with the fewest in-flight requests", () =>
                    {
                        OriginServer a = Origin("a"); a.ActiveRequests = 5;
                        OriginServer b = Origin("b"); b.ActiveRequests = 0;
                        List<OriginServer> origins = new List<OriginServer> { a, b };
                        ApiEndpoint ep = Ep(LoadBalancingMode.LeastConnections, Bind("a"), Bind("b"));
                        OriginServer sel = Select(ep, origins);
                        Check.Equal("b", sel.Identifier, "picked the idle origin, not the busy one");
                    }),

                    Case("PowerOfTwoPicksLessBusy", "Power-of-two-choices picks the less-busy of the sampled pair", () =>
                    {
                        OriginServer a = Origin("a"); a.ActiveRequests = 9;
                        OriginServer b = Origin("b"); b.ActiveRequests = 1;
                        List<OriginServer> origins = new List<OriginServer> { a, b };
                        ApiEndpoint ep = Ep(LoadBalancingMode.PowerOfTwoChoices, Bind("a"), Bind("b"));
                        // With only two candidates both are sampled, so the less-busy one always wins.
                        for (int i = 0; i < 20; i++)
                            Check.Equal("b", Select(ep, origins).Identifier, "less-busy origin chosen");
                    }),

                    Case("LatencyBasedPicksFastest", "Latency mode selects the lowest-EWMA origin", () =>
                    {
                        OriginServer a = Origin("a"); a.EwmaLatencyMs = 200; a.HasLatencySample = true;
                        OriginServer b = Origin("b"); b.EwmaLatencyMs = 20; b.HasLatencySample = true;
                        List<OriginServer> origins = new List<OriginServer> { a, b };
                        ApiEndpoint ep = Ep(LoadBalancingMode.LatencyBased, Bind("a"), Bind("b"));
                        Check.Equal("b", Select(ep, origins).Identifier, "fastest origin chosen");
                    }),

                    Case("LatencyBasedPrefersUnsampled", "Latency mode prefers an origin with no samples yet", () =>
                    {
                        OriginServer a = Origin("a"); a.EwmaLatencyMs = 5; a.HasLatencySample = true;
                        OriginServer c = Origin("c"); // no sample
                        List<OriginServer> origins = new List<OriginServer> { a, c };
                        ApiEndpoint ep = Ep(LoadBalancingMode.LatencyBased, Bind("a"), Bind("c"));
                        Check.Equal("c", Select(ep, origins).Identifier, "unsampled origin chosen to gather data");
                    }),

                    Case("PriorityTierUsedFirst", "Only the lowest priority tier receives traffic while healthy", () =>
                    {
                        List<OriginServer> origins = new List<OriginServer> { Origin("a"), Origin("b") };
                        ApiEndpoint ep = Ep(LoadBalancingMode.RoundRobin, Bind("a", 100, 0), Bind("b", 100, 1));
                        Dictionary<string, int> counts = Distribute(ep, origins, 50, 1);
                        Check.Equal(50, counts["a"], "primary tier serves all");
                        Check.Equal(0, counts.TryGetValue("b", out int cb) ? cb : 0, "backup tier idle while primary healthy");
                    }),

                    Case("PriorityBackupFailover", "The backup tier is used only when the primary tier is gone", () =>
                    {
                        OriginServer a = Origin("a", healthy: false);
                        OriginServer b = Origin("b");
                        List<OriginServer> origins = new List<OriginServer> { a, b };
                        ApiEndpoint ep = Ep(LoadBalancingMode.RoundRobin, Bind("a", 100, 0), Bind("b", 100, 1));
                        Check.Equal("b", Select(ep, origins).Identifier, "backup serves when primary unhealthy");
                    }),

                    Case("StickySameKeySameOrigin", "Sticky sessions pin a key to one origin", () =>
                    {
                        List<OriginServer> origins = new List<OriginServer> { Origin("a"), Origin("b"), Origin("c") };
                        ApiEndpoint ep = Ep(LoadBalancingMode.RoundRobin, Bind("a"), Bind("b"), Bind("c"));
                        ep.StickySessionEnabled = true;
                        ep.StickySessionHeader = "X-Session";
                        NameValueCollection headers = Headers("X-Session", "user-1");
                        string first = OriginSelector.Select(ep, origins, null, headers, null, new Random(1), Now)!.Identifier;
                        for (int i = 0; i < 25; i++)
                        {
                            string again = OriginSelector.Select(ep, origins, null, headers, null, new Random(i + 2), Now)!.Identifier;
                            Check.Equal(first, again, "same key routes to the same origin");
                        }
                    }),

                    Case("StickyFallsBackWhenOriginDown", "Sticky routing falls back to a healthy origin when the pinned one is down", () =>
                    {
                        List<OriginServer> origins = new List<OriginServer> { Origin("a"), Origin("b"), Origin("c") };
                        ApiEndpoint ep = Ep(LoadBalancingMode.RoundRobin, Bind("a"), Bind("b"), Bind("c"));
                        ep.StickySessionEnabled = true;
                        ep.StickySessionHeader = "X-Session";
                        NameValueCollection headers = Headers("X-Session", "user-42");
                        OriginServer pinned = OriginSelector.Select(ep, origins, null, headers, null, new Random(1), Now)!;
                        pinned.Healthy = false;
                        OriginServer next = OriginSelector.Select(ep, origins, null, headers, null, new Random(1), Now)!;
                        Check.True(next.Identifier != pinned.Identifier && next.Healthy, "fell back to a different healthy origin");
                    }),

                    Case("SlowStartLimitsThenRestores", "A warming origin receives reduced traffic, then its full share", () =>
                    {
                        OriginServer a = Origin("a"); a.SlowStartMs = 10000; a.LastHealthyUtc = Now; // just became healthy
                        OriginServer b = Origin("b");
                        List<OriginServer> origins = new List<OriginServer> { a, b };
                        ApiEndpoint ep = Ep(LoadBalancingMode.Weighted, Bind("a", 100), Bind("b", 100));

                        Dictionary<string, int> warming = Distribute(ep, origins, 4000, 5, Now);
                        Check.True(warming["a"] < warming["b"] / 2, "warming origin gets far less traffic (a=" + warming["a"] + ", b=" + warming["b"] + ")");

                        Dictionary<string, int> warmed = Distribute(ep, origins, 4000, 5, Now.AddMilliseconds(10000));
                        double ratio = warmed["a"] / (double)warmed["b"];
                        Check.True(ratio > 0.8 && ratio < 1.2, "after the window the share is balanced (ratio=" + ratio.ToString("F2") + ")");
                    }),

                    Case("EjectedOriginExcluded", "An ejected origin is excluded until its ejection window passes", () =>
                    {
                        OriginServer a = Origin("a"); a.EjectedUntilUtc = Now.AddMilliseconds(5000);
                        OriginServer b = Origin("b");
                        List<OriginServer> origins = new List<OriginServer> { a, b };
                        ApiEndpoint ep = Ep(LoadBalancingMode.RoundRobin, Bind("a"), Bind("b"));

                        for (int i = 0; i < 20; i++)
                            Check.Equal("b", Select(ep, origins).Identifier, "ejected origin skipped");

                        // After the ejection window, the origin is eligible again.
                        Dictionary<string, int> after = Distribute(ep, origins, 20, 1, Now.AddMilliseconds(6000));
                        Check.True(after.TryGetValue("a", out int ca) && ca > 0, "origin returns after ejection window");
                    }),

                    Case("CanaryHeaderForcesMatch", "A matching canary header forces the request to the canary origin", () =>
                    {
                        List<OriginServer> origins = new List<OriginServer> { Origin("stable"), Origin("canary") };
                        ApiEndpoint ep = Ep(LoadBalancingMode.RoundRobin,
                            Bind("stable"),
                            Bind("canary", 100, 0, "X-Canary", "on"));
                        NameValueCollection headers = Headers("X-Canary", "on");
                        for (int i = 0; i < 20; i++)
                            Check.Equal("canary", OriginSelector.Select(ep, origins, null, headers, null, new Random(i), Now)!.Identifier, "canary header routes to canary origin");
                    }),

                    Case("CanaryNoMatchUsesPool", "Without the canary header, routing uses the normal pool", () =>
                    {
                        List<OriginServer> origins = new List<OriginServer> { Origin("stable"), Origin("canary") };
                        ApiEndpoint ep = Ep(LoadBalancingMode.RoundRobin,
                            Bind("stable"),
                            Bind("canary", 100, 0, "X-Canary", "on"));
                        Dictionary<string, int> counts = Distribute(ep, origins, 50, 1);
                        Check.True(counts.TryGetValue("stable", out int cs) && cs > 0, "stable origin still serves normal traffic");
                    }),

                    Case("ExcludeSkipsTriedOrigin", "Excluded origins (already tried during retries) are skipped", () =>
                    {
                        List<OriginServer> origins = new List<OriginServer> { Origin("a"), Origin("b") };
                        ApiEndpoint ep = Ep(LoadBalancingMode.RoundRobin, Bind("a"), Bind("b"));
                        HashSet<string> exclude = new HashSet<string> { "a" };
                        for (int i = 0; i < 20; i++)
                            Check.Equal("b", OriginSelector.Select(ep, origins, null, null, exclude, new Random(i), Now)!.Identifier, "excluded origin never chosen");
                    }),

                    Case("NoHealthyOriginReturnsNull", "Selection returns null when no origin is available", () =>
                    {
                        List<OriginServer> origins = new List<OriginServer> { Origin("a", healthy: false) };
                        ApiEndpoint ep = Ep(LoadBalancingMode.RoundRobin, Bind("a"));
                        OriginServer? sel = OriginSelector.Select(ep, origins, null, null, null, new Random(1), Now);
                        Check.True(sel == null, "null when nothing is available");
                    }),

                    Case("RoundRobinRotates", "Round-robin cycles through the origins in order", () =>
                    {
                        List<OriginServer> origins = new List<OriginServer> { Origin("a"), Origin("b"), Origin("c") };
                        ApiEndpoint ep = Ep(LoadBalancingMode.RoundRobin, Bind("a"), Bind("b"), Bind("c"));
                        Random rng = new Random(1);
                        List<string> seen = new List<string>();
                        for (int i = 0; i < 6; i++)
                            seen.Add(OriginSelector.Select(ep, origins, null, null, null, rng, Now)!.Identifier);
                        Check.Equal("a,b,c,a,b,c", String.Join(",", seen), "rotation order");
                    })
                });
        }

        #region Helpers

        private static TestCaseDescriptor Case(string caseId, string displayName, Action body)
        {
            return new TestCaseDescriptor(
                suiteId: "RoutingUnit",
                caseId: caseId,
                displayName: displayName,
                executeAsync: _ =>
                {
                    body();
                    return Task.CompletedTask;
                });
        }

        private static OriginServer Origin(string id, bool healthy = true)
        {
            OriginServer origin = new OriginServer
            {
                Identifier = id,
                Name = id,
                Hostname = "127.0.0.1",
                Port = 8000
            };
            origin.Healthy = healthy;
            return origin;
        }

        private static OriginBinding Bind(string id, int weight = 100, int priority = 0, string? canaryHeader = null, string? canaryValue = null)
        {
            return new OriginBinding(id)
            {
                Weight = weight,
                Priority = priority,
                CanaryHeader = canaryHeader,
                CanaryValue = canaryValue
            };
        }

        private static ApiEndpoint Ep(LoadBalancingMode mode, params OriginBinding[] bindings)
        {
            ApiEndpoint ep = new ApiEndpoint
            {
                Identifier = "ep",
                LoadBalancing = mode
            };
            ep.OriginBindings = new List<OriginBinding>(bindings);
            ep.OriginServers = bindings.Select(b => b.Identifier).ToList();
            return ep;
        }

        private static NameValueCollection Headers(params string[] keyValuePairs)
        {
            NameValueCollection headers = new NameValueCollection();
            for (int i = 0; i + 1 < keyValuePairs.Length; i += 2)
                headers.Add(keyValuePairs[i], keyValuePairs[i + 1]);
            return headers;
        }

        private static OriginServer Select(ApiEndpoint ep, List<OriginServer> origins)
        {
            return OriginSelector.Select(ep, origins, null, null, null, new Random(1), Now)!;
        }

        private static Dictionary<string, int> Distribute(ApiEndpoint ep, List<OriginServer> origins, int iterations, int seed)
        {
            return Distribute(ep, origins, iterations, seed, Now);
        }

        private static Dictionary<string, int> Distribute(ApiEndpoint ep, List<OriginServer> origins, int iterations, int seed, DateTime nowUtc)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            Random rng = new Random(seed);
            for (int i = 0; i < iterations; i++)
            {
                OriginServer? sel = OriginSelector.Select(ep, origins, null, null, null, rng, nowUtc);
                if (sel == null) continue;
                counts[sel.Identifier] = (counts.TryGetValue(sel.Identifier, out int c) ? c : 0) + 1;
            }
            return counts;
        }

        #endregion
    }
}
