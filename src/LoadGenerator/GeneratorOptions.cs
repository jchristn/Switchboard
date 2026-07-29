namespace LoadGenerator
{
    using System;
    using System.Globalization;
    using System.IO;

    /// <summary>
    /// Parsed command-line options for the load generator, with reasonable defaults applied. All
    /// timestamps are UTC.
    /// </summary>
    public sealed class GeneratorOptions
    {
        #region Public-Members

        /// <summary>
        /// Inclusive start of the window to generate request history for (UTC).
        /// </summary>
        public DateTime StartUtc { get; private set; }

        /// <summary>
        /// Exclusive end of the window to generate request history for (UTC).
        /// </summary>
        public DateTime EndUtc { get; private set; }

        /// <summary>
        /// Average number of requests to synthesize per day. Actual per-day counts vary with weekday,
        /// time of day, and random jitter. Minimum is 1.
        /// </summary>
        public int RequestsPerDay { get; private set; }

        /// <summary>
        /// Path to the SQLite database file to write into.
        /// </summary>
        public string DatabasePath { get; private set; }

        #endregion

        #region Private-Members

        private const int _DefaultRequestsPerDay = 700;
        private const int _DefaultWindowDays = 30;

        #endregion

        #region Constructors-and-Factories

        private GeneratorOptions(DateTime startUtc, DateTime endUtc, int requestsPerDay, string databasePath)
        {
            StartUtc = startUtc;
            EndUtc = endUtc;
            RequestsPerDay = requestsPerDay;
            DatabasePath = databasePath;
        }

        /// <summary>
        /// Parse command-line arguments into options, applying defaults for anything not supplied.
        /// Supports named flags (<c>--start</c>, <c>--end</c>, <c>--density</c>, <c>--db</c>) and, for
        /// convenience, bare positional arguments in the order start, end, density, database path.
        /// </summary>
        /// <param name="args">Raw command-line arguments. Null is treated as empty.</param>
        /// <returns>Parsed options.</returns>
        /// <exception cref="ArgumentException">Thrown when an argument is malformed or the window is empty.</exception>
        public static GeneratorOptions Parse(string[] args)
        {
            DateTime? startArg = null;
            DateTime? endArg = null;
            int? densityArg = null;
            string? dbArg = null;
            int positional = 0;

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string token = args[i];

                    if (token == "--start" || token == "--from")
                    {
                        startArg = ParseDate(NextValue(args, ref i, token));
                    }
                    else if (token == "--end" || token == "--to")
                    {
                        endArg = ParseDate(NextValue(args, ref i, token));
                    }
                    else if (token == "--density" || token == "--per-day" || token == "-d")
                    {
                        densityArg = ParseDensity(NextValue(args, ref i, token));
                    }
                    else if (token == "--db" || token == "--database")
                    {
                        dbArg = NextValue(args, ref i, token);
                    }
                    else if (token.StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException("Unknown option '" + token + "'.");
                    }
                    else
                    {
                        switch (positional)
                        {
                            case 0: startArg = ParseDate(token); break;
                            case 1: endArg = ParseDate(token); break;
                            case 2: densityArg = ParseDensity(token); break;
                            case 3: dbArg = token; break;
                            default: throw new ArgumentException("Unexpected argument '" + token + "'.");
                        }
                        positional++;
                    }
                }
            }

            DateTime endUtc = endArg ?? DateTime.UtcNow;
            DateTime startUtc = startArg ?? endUtc.AddDays(-_DefaultWindowDays);
            int density = densityArg ?? _DefaultRequestsPerDay;
            string databasePath = !String.IsNullOrWhiteSpace(dbArg) ? dbArg! : ResolveDefaultDatabasePath();

            if (startUtc >= endUtc)
                throw new ArgumentException("Start (" + startUtc.ToString("o") + ") must be earlier than end (" + endUtc.ToString("o") + ").");

            return new GeneratorOptions(startUtc, endUtc, density, databasePath);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Usage text describing the supported command-line arguments.
        /// </summary>
        /// <returns>Multi-line usage string.</returns>
        public static string UsageText()
        {
            return
                "Usage: LoadGenerator [--start <date>] [--end <date>] [--density <requests-per-day>] [--db <path>]" + Environment.NewLine +
                Environment.NewLine +
                "  --start, --from     Window start (e.g. 2026-06-28). Default: 30 days before end." + Environment.NewLine +
                "  --end, --to         Window end (e.g. 2026-07-28). Default: now (UTC)." + Environment.NewLine +
                "  --density, -d       Average requests per day. Default: " + _DefaultRequestsPerDay + "." + Environment.NewLine +
                "  --db, --database    SQLite database path. Default: the deployment's Docker/data/switchboard.db if found," + Environment.NewLine +
                "                      otherwise ./switchboard.db." + Environment.NewLine +
                Environment.NewLine +
                "Positional form: LoadGenerator <start> <end> <density> <db>";
        }

        #endregion

        #region Private-Methods

        private static string NextValue(string[] args, ref int index, string flag)
        {
            if (index + 1 >= args.Length)
                throw new ArgumentException("Option '" + flag + "' requires a value.");
            index++;
            return args[index];
        }

        private static DateTime ParseDate(string value)
        {
            if (!DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out DateTime parsed))
            {
                throw new ArgumentException("Invalid date '" + value + "'. Use a form such as 2026-06-28.");
            }

            return parsed;
        }

        private static int ParseDensity(string value)
        {
            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed < 1)
                throw new ArgumentException("Invalid density '" + value + "'. Use a positive integer.");
            return parsed;
        }

        // Walk up from the current directory looking for a deployed SQLite database at
        // Docker/data/switchboard.db, so running from anywhere in the repository targets the live
        // deployment by default. Falls back to ./switchboard.db when nothing is found.
        private static string ResolveDefaultDatabasePath()
        {
            DirectoryInfo? dir = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "Docker", "data", "switchboard.db");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            return Path.Combine(Directory.GetCurrentDirectory(), "switchboard.db");
        }

        #endregion
    }
}
