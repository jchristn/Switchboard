namespace LoadGenerator
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Switchboard.Core.Client;

    /// <summary>
    /// Console entry point for the synthetic load generator. Seeds an example topology and writes
    /// realistic request-history rows into the Switchboard database so the dashboard can be
    /// demonstrated and screenshotted with lifelike data.
    /// </summary>
    public static class Program
    {
        #region Public-Methods

        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Command-line arguments. See <see cref="GeneratorOptions.UsageText"/>.</param>
        /// <returns>Process exit code: 0 on success, 1 on a usage or runtime error.</returns>
        public static async Task<int> Main(string[] args)
        {
            if (HasHelpFlag(args))
            {
                Console.WriteLine(GeneratorOptions.UsageText());
                return 0;
            }

            GeneratorOptions options;
            try
            {
                options = GeneratorOptions.Parse(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                Console.Error.WriteLine();
                Console.Error.WriteLine(GeneratorOptions.UsageText());
                return 1;
            }

            int windowDays = (int)Math.Round((options.EndUtc - options.StartUtc).TotalDays);

            Console.Error.WriteLine("LoadGenerator");
            Console.Error.WriteLine("  Database: " + options.DatabasePath);
            Console.Error.WriteLine("  Window:   " + options.StartUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                + " .. " + options.EndUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC");
            Console.Error.WriteLine("  Density:  ~" + options.RequestsPerDay.ToString("N0", CultureInfo.InvariantCulture) + " requests/day");
            Console.Error.WriteLine("  Generating...");

            Stopwatch stopwatch = Stopwatch.StartNew();

            SwitchboardClient client = await SwitchboardClient.CreateAsync(options.DatabasePath).ConfigureAwait(false);
            try
            {
                await client.Database.InitializeSchemaAsync(CancellationToken.None).ConfigureAwait(false);

                SyntheticDataGenerator generator = new SyntheticDataGenerator(client, options, new Random());
                await generator.RunAsync(CancellationToken.None).ConfigureAwait(false);

                stopwatch.Stop();
                Console.Out.Write(BuildSummary(options, generator, windowDays, stopwatch.Elapsed));
                return 0;
            }
            finally
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }

        #endregion

        #region Private-Methods

        private static bool HasHelpFlag(string[] args)
        {
            if (args == null) return false;
            foreach (string arg in args)
            {
                if (arg == "--help" || arg == "-h" || arg == "-?" || arg == "/?") return true;
            }
            return false;
        }

        private static string BuildSummary(GeneratorOptions options, SyntheticDataGenerator generator, int windowDays, TimeSpan elapsed)
        {
            long total = generator.HistoryCreated;
            double successPct = total > 0 ? (generator.SuccessCount * 100.0 / total) : 0.0;
            double failPct = total > 0 ? (generator.FailureCount * 100.0 / total) : 0.0;

            StringBuilder sb = new StringBuilder();
            sb.Append(Environment.NewLine);
            sb.Append("LoadGenerator complete.").Append(Environment.NewLine);
            sb.Append("  Database:          ").Append(options.DatabasePath).Append(Environment.NewLine);
            sb.Append("  Window:            ")
              .Append(options.StartUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
              .Append(" .. ")
              .Append(options.EndUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
              .Append(" (").Append(windowDays.ToString(CultureInfo.InvariantCulture)).Append(" days, UTC)")
              .Append(Environment.NewLine);
            sb.Append("  Target density:    ~").Append(options.RequestsPerDay.ToString("N0", CultureInfo.InvariantCulture)).Append(" requests/day").Append(Environment.NewLine);
            sb.Append("  Origins created:   ").Append(generator.OriginsCreated.ToString(CultureInfo.InvariantCulture)).Append(Environment.NewLine);
            sb.Append("  Endpoints created: ").Append(generator.EndpointsCreated.ToString(CultureInfo.InvariantCulture)).Append(Environment.NewLine);
            sb.Append("  Routes created:    ").Append(generator.RoutesCreated.ToString(CultureInfo.InvariantCulture)).Append(Environment.NewLine);
            sb.Append("  Mappings created:  ").Append(generator.MappingsCreated.ToString(CultureInfo.InvariantCulture)).Append(Environment.NewLine);
            sb.Append("  Request history:   ").Append(total.ToString("N0", CultureInfo.InvariantCulture)).Append(" rows").Append(Environment.NewLine);
            sb.Append("    Successful (2xx/3xx): ")
              .Append(generator.SuccessCount.ToString("N0", CultureInfo.InvariantCulture))
              .Append("  (").Append(successPct.ToString("F1", CultureInfo.InvariantCulture)).Append("%)")
              .Append(Environment.NewLine);
            sb.Append("    Failed     (4xx/5xx): ")
              .Append(generator.FailureCount.ToString("N0", CultureInfo.InvariantCulture))
              .Append("  (").Append(failPct.ToString("F1", CultureInfo.InvariantCulture)).Append("%)")
              .Append(Environment.NewLine);
            sb.Append("  Elapsed:           ").Append(FormatElapsed(elapsed)).Append(Environment.NewLine);
            sb.Append(Environment.NewLine);
            return sb.ToString();
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalMinutes >= 1.0)
                return ((int)elapsed.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m " + elapsed.Seconds.ToString(CultureInfo.InvariantCulture) + "s";
            return elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
        }

        #endregion
    }
}
