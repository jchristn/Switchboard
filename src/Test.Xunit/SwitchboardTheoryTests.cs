namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;

    using Test.Shared;
    using Touchstone.Core;
    using global::Xunit;
    using global::Xunit.Abstractions;

    /// <summary>
    /// Theory-style host: each non-skipped descriptor becomes a separate theory row for per-test
    /// visibility in the IDE Test Explorer.
    /// </summary>
    public sealed class SwitchboardTheoryTests
    {
        private readonly ITestOutputHelper _Output;

        /// <summary>
        /// Initialize with the xUnit output helper.
        /// </summary>
        /// <param name="output">Test output helper.</param>
        public SwitchboardTheoryTests(ITestOutputHelper output)
        {
            _Output = output;
        }

        /// <summary>
        /// Provides non-skipped descriptors as theory data.
        /// </summary>
        /// <returns>Theory data rows.</returns>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();

            foreach (TestSuiteDescriptor suite in SwitchboardSuites.All)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip) data.Add(testCase);
                }
            }

            return data;
        }

        /// <summary>
        /// Execute a single descriptor.
        /// </summary>
        /// <param name="testCase">Test case to run.</param>
        /// <returns>Task.</returns>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            _Output.WriteLine("Running: " + testCase.DisplayName);
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
