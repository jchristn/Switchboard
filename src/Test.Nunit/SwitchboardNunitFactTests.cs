namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Fact-style NUnit host: all descriptors run in a single test, honoring suite lifecycle hooks.
    /// </summary>
    [TestFixture]
    public sealed class SwitchboardNunitFactTests : TouchstoneNunitBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return SwitchboardSuites.All; }
        }

        /// <summary>
        /// Run all shared descriptors as a single test.
        /// </summary>
        /// <returns>Task.</returns>
        [Test]
        public async Task RunAll()
        {
            await RunAllAsync().ConfigureAwait(false);
        }
    }
}
