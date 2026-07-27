namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;

    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Data-driven NUnit host: each descriptor becomes a separate test case via TestCaseSource.
    /// </summary>
    [TestFixture]
    public sealed class SwitchboardNunitTests
    {
        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(SwitchboardSuites.All);
        }

        /// <summary>
        /// Execute a single descriptor.
        /// </summary>
        /// <param name="testCase">Test case to run.</param>
        /// <returns>Task.</returns>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
