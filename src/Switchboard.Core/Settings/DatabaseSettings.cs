#nullable enable

namespace Switchboard.Core.Settings
{
    using System;
    using Switchboard.Core.Database;

    /// <summary>
    /// Database configuration settings.
    /// </summary>
    public class DatabaseSettings
    {
        #region Public-Members

        /// <summary>
        /// Database type.
        /// Default is Sqlite.
        /// </summary>
        public DatabaseTypeEnum Type { get; set; } = DatabaseTypeEnum.Sqlite;

        /// <summary>
        /// Connection string.
        /// If provided, this takes precedence over individual connection parameters.
        /// </summary>
        public string? ConnectionString { get; set; } = null;

        /// <summary>
        /// Database file path (for SQLite only).
        /// Default is "sb.db".
        /// </summary>
        public string Filename
        {
            get => _Filename;
            set
            {
                if (String.IsNullOrWhiteSpace(value)) value = "sb.db";
                _Filename = value;
            }
        }

        /// <summary>
        /// Database hostname (for MySQL, PostgreSQL, SQL Server).
        /// </summary>
        public string? Hostname { get; set; } = null;

        /// <summary>
        /// Database port.
        /// Default values: MySQL=3306, PostgreSQL=5432, SQL Server=1433.
        /// </summary>
        public int? Port { get; set; } = null;

        /// <summary>
        /// Database name.
        /// </summary>
        public string? DatabaseName { get; set; } = null;

        /// <summary>
        /// Database username.
        /// </summary>
        public string? Username { get; set; } = null;

        /// <summary>
        /// Database password.
        /// </summary>
        public string? Password { get; set; } = null;

        /// <summary>
        /// Enable SSL/TLS for database connection.
        /// Default is false.
        /// </summary>
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// Trust server certificate (for SSL connections).
        /// Default is false.
        /// </summary>
        public bool TrustServerCertificate { get; set; } = false;

        #endregion

        #region Private-Members

        private string _Filename = "sb.db";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseSettings()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build connection string from settings.
        /// </summary>
        /// <returns>Connection string.</returns>
        public string BuildConnectionString()
        {
            if (!String.IsNullOrWhiteSpace(ConnectionString))
                return ConnectionString;

            switch (Type)
            {
                case DatabaseTypeEnum.Sqlite:
                    return $"Data Source={Filename}";

                case DatabaseTypeEnum.Mysql:
                    int mysqlPort = Port ?? 3306;
                    string mysqlSsl = Ssl ? "true" : "false";
                    return $"Server={Hostname};Port={mysqlPort};Database={DatabaseName};User={Username};Password={Password};SslMode={(Ssl ? "Required" : "None")}";

                case DatabaseTypeEnum.Postgres:
                    int pgPort = Port ?? 5432;
                    string pgSsl = Ssl ? "Require" : "Disable";
                    string pgTrust = TrustServerCertificate ? ";Trust Server Certificate=true" : "";
                    return $"Host={Hostname};Port={pgPort};Database={DatabaseName};Username={Username};Password={Password};SSL Mode={pgSsl}{pgTrust}";

                case DatabaseTypeEnum.SqlServer:
                    int sqlPort = Port ?? 1433;
                    string sqlServer = Port.HasValue ? $"{Hostname},{sqlPort}" : Hostname ?? "localhost";
                    string sqlEncrypt = Ssl ? "true" : "false";
                    string sqlTrust = TrustServerCertificate ? "true" : "false";
                    return $"Server={sqlServer};Database={DatabaseName};User Id={Username};Password={Password};Encrypt={sqlEncrypt};TrustServerCertificate={sqlTrust}";

                default:
                    throw new ArgumentException($"Unsupported database type: {Type}");
            }
        }

        #endregion
    }
}
