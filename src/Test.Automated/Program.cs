namespace Test.Automated
{
    using System.Threading.Tasks;

    using Test.Shared;
    using Touchstone.Cli;

    /// <summary>
    /// Console entry point that executes all shared Switchboard test suites through the
    /// Touchstone console runner. Supports optional JSON export via <c>--results &lt;path&gt;</c>.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">Command line arguments. Use <c>--results &lt;path&gt;</c> to export JSON.</param>
        /// <returns>Exit code: 0 when all tests pass, non-zero when any test fails.</returns>
        public static async Task<int> Main(string[] args)
        {
            string? resultsPath = null;
            bool unitOnly = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--results" && i + 1 < args.Length)
                {
                    resultsPath = args[i + 1];
                }
                else if (args[i] == "--unit")
                {
                    unitOnly = true;
                }
            }

            return await ConsoleRunner.RunAsync(
                unitOnly ? SwitchboardSuites.UnitOnly : SwitchboardSuites.All,
                resultsPath: resultsPath).ConfigureAwait(false);
        }
    }
}
