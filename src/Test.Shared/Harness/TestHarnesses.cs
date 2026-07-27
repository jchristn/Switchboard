namespace Test.Shared.Harness
{
    using System.Collections.Generic;

    /// <summary>
    /// Process-wide harness singletons shared across every test suite and runner. The shared proxy
    /// harness is read-only (its origins are never stopped by a test) so its state is stable and it
    /// can safely back concurrent, order-independent test cases. Destructive scenarios (health
    /// transitions, rate limiting) create their own dedicated harnesses on separate ports.
    /// </summary>
    public static class TestHarnesses
    {
        /// <summary>
        /// Shared always-healthy proxy on port 9200 with four origins on 9201-9204. Rate limits are
        /// intentionally generous so ordinary functional tests never trip them.
        /// </summary>
        public static readonly ProxyHarness Shared = new ProxyHarness(
            9200,
            new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("Server 1", 9201),
                new KeyValuePair<string, int>("Server 2", 9202),
                new KeyValuePair<string, int>("Server 3", 9203),
                new KeyValuePair<string, int>("Server 4", 9204)
            },
            maxParallelRequests: 100,
            rateLimitThreshold: 1000);

        /// <summary>
        /// Dedicated harness (proxy 9260, origins 9261-9262) for gateway error scenarios that mutate
        /// endpoint configuration (HTTP/1.0 blocking, request-size limits, auth-callback overrides).
        /// Kept separate from <see cref="Shared"/> so those mutations never affect other suites.
        /// </summary>
        public static readonly ProxyHarness GatewayErrors = new ProxyHarness(
            9260,
            new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("Server 1", 9261),
                new KeyValuePair<string, int>("Server 2", 9262)
            });

        /// <summary>
        /// Dedicated harness (proxy 9270, origins 9271-9272) with the management REST API enabled.
        /// </summary>
        public static readonly ProxyHarness Management = new ProxyHarness(
            9270,
            new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("Server 1", 9271),
                new KeyValuePair<string, int>("Server 2", 9272)
            },
            enableManagement: true);
    }
}
