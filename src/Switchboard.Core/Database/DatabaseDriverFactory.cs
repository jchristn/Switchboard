#nullable enable

namespace Switchboard.Core.Database
{
    using System;
    using Switchboard.Core.Database.Mysql;
    using Switchboard.Core.Database.Postgres;
    using Switchboard.Core.Database.Sqlite;
    using Switchboard.Core.Database.SqlServer;

    /// <summary>
    /// Factory for creating database driver instances.
    /// </summary>
    public static class DatabaseDriverFactory
    {
        #region Public-Methods

        /// <summary>
        /// Create a database driver from a connection string.
        /// </summary>
        /// <param name="type">Database type.</param>
        /// <param name="connectionString">Connection string.</param>
        /// <returns>Database driver instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if connectionString is null or empty.</exception>
        /// <exception cref="NotSupportedException">Thrown if database type is not supported.</exception>
        public static IDatabaseDriver Create(DatabaseTypeEnum type, string connectionString)
        {
            if (String.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            return type switch
            {
                DatabaseTypeEnum.Sqlite => new SqliteDatabaseDriver(connectionString),
                DatabaseTypeEnum.Mysql => new MysqlDatabaseDriver(connectionString),
                DatabaseTypeEnum.Postgres => new PostgresDatabaseDriver(connectionString),
                DatabaseTypeEnum.SqlServer => new SqlServerDatabaseDriver(connectionString),
                _ => throw new NotSupportedException($"Database type {type} is not supported.")
            };
        }

        /// <summary>
        /// Create a SQLite database driver.
        /// </summary>
        /// <param name="databasePathOrConnectionString">Path to the SQLite database file, or a connection string.</param>
        /// <returns>SQLite database driver instance.</returns>
        public static IDatabaseDriver CreateSqlite(string databasePathOrConnectionString)
        {
            return new SqliteDatabaseDriver(databasePathOrConnectionString);
        }

        /// <summary>
        /// Create a MySQL database driver.
        /// </summary>
        /// <param name="host">Database host.</param>
        /// <param name="port">Database port.</param>
        /// <param name="database">Database name.</param>
        /// <param name="username">Username.</param>
        /// <param name="password">Password.</param>
        /// <param name="sslMode">SSL mode (None, Preferred, Required).</param>
        /// <returns>MySQL database driver instance.</returns>
        public static IDatabaseDriver CreateMysql(
            string host,
            int port,
            string database,
            string username,
            string password,
            string sslMode = "Preferred")
        {
            return new MysqlDatabaseDriver(host, port, database, username, password, sslMode);
        }

        /// <summary>
        /// Create a PostgreSQL database driver.
        /// </summary>
        /// <param name="host">Database host.</param>
        /// <param name="port">Database port.</param>
        /// <param name="database">Database name.</param>
        /// <param name="username">Username.</param>
        /// <param name="password">Password.</param>
        /// <param name="sslMode">SSL mode (Disable, Prefer, Require).</param>
        /// <returns>PostgreSQL database driver instance.</returns>
        public static IDatabaseDriver CreatePostgres(
            string host,
            int port,
            string database,
            string username,
            string password,
            string sslMode = "Prefer")
        {
            return new PostgresDatabaseDriver(host, port, database, username, password, sslMode);
        }

        /// <summary>
        /// Create a SQL Server database driver.
        /// </summary>
        /// <param name="host">Database host.</param>
        /// <param name="port">Database port (default 1433).</param>
        /// <param name="database">Database name.</param>
        /// <param name="username">Username (null for Windows auth).</param>
        /// <param name="password">Password.</param>
        /// <param name="trustServerCertificate">Trust server certificate.</param>
        /// <returns>SQL Server database driver instance.</returns>
        public static IDatabaseDriver CreateSqlServer(
            string host,
            int port,
            string database,
            string? username,
            string? password,
            bool trustServerCertificate = false)
        {
            return new SqlServerDatabaseDriver(host, port, database, username, password, trustServerCertificate);
        }

        #endregion
    }
}
