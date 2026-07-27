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
    }
}
