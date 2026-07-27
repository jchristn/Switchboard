namespace Test.Xunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using global::Xunit;

    /// <summary>
    /// Fact-style host: runs every shared descriptor sequentially through the Touchstone executor,
    /// honoring suite lifecycle hooks.
    /// </summary>
    public sealed class SwitchboardFactTests : TouchstoneFactBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return SwitchboardSuites.All; }
        }

        /// <summary>
        /// Run all shared descriptors as a single fact.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
