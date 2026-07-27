namespace Test.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using SerializationHelper;
    using Test.Shared.Harness;
    using Touchstone.Core;

    /// <summary>
    /// Shared helpers for integration suites that run against the always-healthy
    /// <see cref="TestHarnesses.Shared"/> proxy harness.
    /// </summary>
    public static class IntegrationSupport
    {
        /// <summary>
        /// A JSON serializer for building request payloads.
        /// </summary>
        public static readonly Serializer Json = new Serializer();

        /// <summary>
        /// A suite lifecycle hook that ensures the shared harness is running before any case executes.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Completed value task once the harness is healthy.</returns>
        public static ValueTask EnsureSharedStarted(CancellationToken token)
        {
            return new ValueTask(TestHarnesses.Shared.StartAsync(token));
        }

        /// <summary>
        /// Build a test case that runs against the shared harness, ensuring it is started first so
        /// the case is self-contained even when suite lifecycle hooks are bypassed (theory runners).
        /// </summary>
        /// <param name="suiteId">Suite identifier.</param>
        /// <param name="caseId">Case identifier.</param>
        /// <param name="displayName">Human-readable case name.</param>
        /// <param name="body">Case body receiving the shared harness and cancellation token.</param>
        /// <returns>A test case descriptor.</returns>
        public static TestCaseDescriptor SharedCase(
            string suiteId,
            string caseId,
            string displayName,
            Func<ProxyHarness, CancellationToken, Task> body)
        {
            return new TestCaseDescriptor(
                suiteId: suiteId,
                caseId: caseId,
                displayName: displayName,
                executeAsync: async ct =>
                {
                    await TestHarnesses.Shared.StartAsync(ct).ConfigureAwait(false);
                    await body(TestHarnesses.Shared, ct).ConfigureAwait(false);
                });
        }
    }
}
