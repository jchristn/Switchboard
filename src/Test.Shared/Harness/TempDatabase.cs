namespace Test.Shared.Harness
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using Switchboard.Core.Client;
    using Switchboard.Core.Database;

    /// <summary>
    /// A throwaway SQLite database with an initialized schema and a connected
    /// <see cref="SwitchboardClient"/>. Each instance uses a unique temp file that is deleted on
    /// dispose. Network-free; suitable for exercising the client and services directly.
    /// </summary>
    public sealed class TempDatabase
    {
        /// <summary>
        /// Path to the temporary SQLite database file.
        /// </summary>
        public string DbPath { get; private set; } = string.Empty;

        /// <summary>
        /// The open database driver.
        /// </summary>
        public IDatabaseDriver Driver { get; private set; } = null!;

        /// <summary>
        /// A client over the open driver.
        /// </summary>
        public SwitchboardClient Client { get; private set; } = null!;

        /// <summary>
        /// Create and initialize a new temporary database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An initialized <see cref="TempDatabase"/>.</returns>
        public static async Task<TempDatabase> CreateAsync(CancellationToken token = default)
        {
            string dbPath = Path.Combine(Path.GetTempPath(), "switchboard_test_" + Guid.NewGuid().ToString("N") + ".db");
            IDatabaseDriver driver = DatabaseDriverFactory.Create(DatabaseTypeEnum.Sqlite, "Data Source=" + dbPath);
            await driver.OpenAsync(token).ConfigureAwait(false);
            await driver.InitializeSchemaAsync(token).ConfigureAwait(false);

            return new TempDatabase
            {
                DbPath = dbPath,
                Driver = driver,
                Client = new SwitchboardClient(driver)
            };
        }

        /// <summary>
        /// Dispose the client and driver and delete the database file.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task DisposeAsync()
        {
            try
            {
                Client?.Dispose();
            }
            catch (Exception)
            {
                // ignore
            }

            try
            {
                if (Driver != null) await Driver.CloseAsync().ConfigureAwait(false);
                Driver?.Dispose();
            }
            catch (Exception)
            {
                // ignore
            }

            try
            {
                if (File.Exists(DbPath)) File.Delete(DbPath);
            }
            catch (Exception)
            {
                // best-effort
            }
        }
    }
}
