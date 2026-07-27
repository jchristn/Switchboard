namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Linq;

    using Touchstone.Core;

    /// <summary>
    /// The complete set of Switchboard test suites. Every runner (console, xUnit, NUnit) consumes
    /// <see cref="All"/> so the four test projects share a single source of truth for coverage.
    /// </summary>
    public static class SwitchboardSuites
    {
        /// <summary>
        /// All suites: network-free unit suites first, then database and integration suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>();
                suites.AddRange(UnitSuites.All);
                suites.AddRange(SettingsImportSuites.All);
                suites.AddRange(ProxySuites.All);
                suites.AddRange(StreamingSuites.All);
                suites.AddRange(HealthSuites.All);
                return suites;
            }
        }

        /// <summary>
        /// Only the network-free suites (unit + database). Useful for fast, isolated runs that do
        /// not bind TCP ports.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> UnitOnly
        {
            get { return UnitSuites.All.Concat(SettingsImportSuites.All).ToList(); }
        }
    }
}
