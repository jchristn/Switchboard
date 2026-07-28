namespace Test.Shared.Harness
{
    using System.Collections.Generic;

    /// <summary>
    /// Process-wide harness singletons shared across every test suite and runner. The shared proxy
    /// harness is read-only (its origins are never stopped by a test) so its state is stable and it
    /// can safely back concurrent, order-independent test cases. Destructive scenarios (health
    /// transitions, rate limiting) create their own dedicated harnesses. Every harness binds a random
    /// free localhost port for the proxy and for each origin when it starts, so runs never collide on
    /// a fixed port.
    /// </summary>
    public static class TestHarnesses
    {
        /// <summary>
        /// Shared always-healthy proxy with four origins on random ports. Rate limits are intentionally
        /// generous so ordinary functional tests never trip them.
        /// </summary>
        public static readonly ProxyHarness Shared = new ProxyHarness(
            new List<string> { "Server 1", "Server 2", "Server 3", "Server 4" },
            maxParallelRequests: 100,
            rateLimitThreshold: 1000);

        /// <summary>
        /// Dedicated harness with two origins for gateway error scenarios that mutate endpoint
        /// configuration (HTTP/1.0 blocking, request-size limits, auth-callback overrides). Kept
        /// separate from <see cref="Shared"/> so those mutations never affect other suites.
        /// </summary>
        public static readonly ProxyHarness GatewayErrors = new ProxyHarness(
            new List<string> { "Server 1", "Server 2" });

        /// <summary>
        /// Dedicated harness with two origins and the management REST API enabled.
        /// </summary>
        public static readonly ProxyHarness Management = new ProxyHarness(
            new List<string> { "Server 1", "Server 2" },
            enableManagement: true);
    }
}
