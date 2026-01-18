#nullable enable

namespace Switchboard.Core.Database.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Switchboard.Core.Models;

    /// <summary>
    /// SQLite database driver implementation.
    /// </summary>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        #region Public-Members

        /// <inheritdoc />
        public override bool IsOpen => _Connection?.State == ConnectionState.Open;

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType => DatabaseTypeEnum.Sqlite;

        #endregion

        #region Private-Members

        private SqliteConnection? _Connection = null;
        private readonly string _DatabasePath;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate SQLite database driver.
        /// </summary>
        /// <param name="databasePathOrConnectionString">Path to the SQLite database file, or a connection string.</param>
        public SqliteDatabaseDriver(string databasePathOrConnectionString)
        {
            if (String.IsNullOrWhiteSpace(databasePathOrConnectionString))
                throw new ArgumentNullException(nameof(databasePathOrConnectionString));

            // Check if input is a connection string (contains "Data Source=")
            if (databasePathOrConnectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                // Parse the Data Source value from the connection string
                string dataSourceValue = ExtractDataSource(databasePathOrConnectionString);
                _DatabasePath = dataSourceValue;

                // Use the provided connection string if it has other settings, otherwise build our own
                if (databasePathOrConnectionString.Contains(";"))
                {
                    ConnectionString = databasePathOrConnectionString;
                }
                else
                {
                    ConnectionString = $"Data Source={dataSourceValue};Cache=Shared";
                }
            }
            else
            {
                // Input is a plain file path
                _DatabasePath = databasePathOrConnectionString;
                ConnectionString = $"Data Source={databasePathOrConnectionString};Cache=Shared";
            }
        }

        /// <summary>
        /// Extracts the Data Source value from a SQLite connection string.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        /// <returns>Data Source value.</returns>
        private static string ExtractDataSource(string connectionString)
        {
            // Split by semicolon and find the Data Source part
            string[] parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring("Data Source=".Length).Trim();
                }
            }
            throw new ArgumentException("Connection string does not contain a Data Source value.", nameof(connectionString));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override async Task OpenAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (IsOpen) return;

            _Connection = new SqliteConnection(ConnectionString);
            await _Connection.OpenAsync(token).ConfigureAwait(false);

            // Enable WAL mode for better concurrency
            using (SqliteCommand cmd = _Connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            await InitializeSchemaAsync(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task CloseAsync(CancellationToken token = default)
        {
            if (_Connection != null)
            {
                if (_Connection.State == ConnectionState.Open)
                {
                    await _Connection.CloseAsync().ConfigureAwait(false);
                }
                await _Connection.DisposeAsync().ConfigureAwait(false);
                _Connection = null;
            }
        }

        /// <inheritdoc />
        public override async Task InitializeSchemaAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();

            string sql = @"
                -- Schema version tracking
                CREATE TABLE IF NOT EXISTS schema_version (
                    version INTEGER PRIMARY KEY,
                    applied_utc TEXT NOT NULL,
                    description TEXT
                );

                -- User master
                CREATE TABLE IF NOT EXISTS user_master (
                    guid TEXT PRIMARY KEY,
                    username TEXT NOT NULL UNIQUE,
                    email TEXT,
                    first_name TEXT,
                    last_name TEXT,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_admin INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    modified_utc TEXT,
                    last_login_utc TEXT
                );

                -- Credentials (bearer tokens)
                CREATE TABLE IF NOT EXISTS credentials (
                    guid TEXT PRIMARY KEY,
                    user_guid TEXT NOT NULL,
                    name TEXT,
                    description TEXT,
                    bearer_token_hash TEXT NOT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_read_only INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    expires_utc TEXT,
                    last_used_utc TEXT,
                    FOREIGN KEY (user_guid) REFERENCES user_master(guid) ON DELETE CASCADE
                );

                -- Origin servers
                CREATE TABLE IF NOT EXISTS origin_servers (
                    identifier TEXT PRIMARY KEY,
                    name TEXT,
                    hostname TEXT NOT NULL,
                    port INTEGER NOT NULL,
                    ssl INTEGER NOT NULL DEFAULT 0,
                    health_check_interval_ms INTEGER NOT NULL DEFAULT 5000,
                    health_check_method TEXT NOT NULL DEFAULT 'HEAD',
                    health_check_url TEXT NOT NULL DEFAULT '/',
                    unhealthy_threshold INTEGER NOT NULL DEFAULT 2,
                    healthy_threshold INTEGER NOT NULL DEFAULT 1,
                    max_parallel_requests INTEGER NOT NULL DEFAULT 10,
                    rate_limit_requests_threshold INTEGER NOT NULL DEFAULT 30,
                    log_request INTEGER NOT NULL DEFAULT 0,
                    log_request_body INTEGER NOT NULL DEFAULT 0,
                    log_response INTEGER NOT NULL DEFAULT 0,
                    log_response_body INTEGER NOT NULL DEFAULT 0,
                    capture_request_body INTEGER NOT NULL DEFAULT 0,
                    capture_response_body INTEGER NOT NULL DEFAULT 0,
                    capture_request_headers INTEGER NOT NULL DEFAULT 1,
                    capture_response_headers INTEGER NOT NULL DEFAULT 1,
                    max_capture_request_body_size INTEGER NOT NULL DEFAULT 65536,
                    max_capture_response_body_size INTEGER NOT NULL DEFAULT 65536,
                    created_utc TEXT NOT NULL,
                    modified_utc TEXT
                );

                -- API endpoints
                CREATE TABLE IF NOT EXISTS api_endpoints (
                    identifier TEXT PRIMARY KEY,
                    name TEXT,
                    timeout_ms INTEGER NOT NULL DEFAULT 60000,
                    load_balancing_mode TEXT NOT NULL DEFAULT 'RoundRobin',
                    block_http10 INTEGER NOT NULL DEFAULT 0,
                    max_request_body_size INTEGER NOT NULL DEFAULT 536870912,
                    log_request_full INTEGER NOT NULL DEFAULT 0,
                    log_request_body INTEGER NOT NULL DEFAULT 0,
                    log_response_body INTEGER NOT NULL DEFAULT 0,
                    include_auth_context_header INTEGER NOT NULL DEFAULT 1,
                    auth_context_header TEXT DEFAULT 'x-sb-auth-context',
                    use_global_blocked_headers INTEGER NOT NULL DEFAULT 1,
                    capture_request_body INTEGER NOT NULL DEFAULT 0,
                    capture_response_body INTEGER NOT NULL DEFAULT 0,
                    capture_request_headers INTEGER NOT NULL DEFAULT 1,
                    capture_response_headers INTEGER NOT NULL DEFAULT 1,
                    max_capture_request_body_size INTEGER NOT NULL DEFAULT 65536,
                    max_capture_response_body_size INTEGER NOT NULL DEFAULT 65536,
                    created_utc TEXT NOT NULL,
                    modified_utc TEXT
                );

                -- Endpoint-origin mappings
                CREATE TABLE IF NOT EXISTS endpoint_origin_mappings (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    endpoint_identifier TEXT NOT NULL,
                    origin_identifier TEXT NOT NULL,
                    sort_order INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (endpoint_identifier) REFERENCES api_endpoints(identifier) ON DELETE CASCADE,
                    FOREIGN KEY (origin_identifier) REFERENCES origin_servers(identifier) ON DELETE CASCADE,
                    UNIQUE (endpoint_identifier, origin_identifier)
                );

                -- Endpoint routes
                CREATE TABLE IF NOT EXISTS endpoint_routes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    endpoint_identifier TEXT NOT NULL,
                    http_method TEXT NOT NULL,
                    url_pattern TEXT NOT NULL,
                    requires_authentication INTEGER NOT NULL DEFAULT 0,
                    sort_order INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (endpoint_identifier) REFERENCES api_endpoints(identifier) ON DELETE CASCADE
                );

                -- URL rewrite rules
                CREATE TABLE IF NOT EXISTS url_rewrites (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    endpoint_identifier TEXT NOT NULL,
                    http_method TEXT NOT NULL,
                    source_pattern TEXT NOT NULL,
                    target_pattern TEXT NOT NULL,
                    FOREIGN KEY (endpoint_identifier) REFERENCES api_endpoints(identifier) ON DELETE CASCADE
                );

                -- Blocked headers
                CREATE TABLE IF NOT EXISTS blocked_headers (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    header_name TEXT NOT NULL UNIQUE
                );

                -- Request history
                CREATE TABLE IF NOT EXISTS request_history (
                    request_id TEXT PRIMARY KEY,
                    timestamp_utc TEXT NOT NULL,
                    http_method TEXT NOT NULL,
                    request_path TEXT NOT NULL,
                    query_string TEXT,
                    endpoint_identifier TEXT,
                    origin_identifier TEXT,
                    client_ip TEXT,
                    request_body_size INTEGER NOT NULL DEFAULT 0,
                    request_body TEXT,
                    request_headers TEXT,
                    status_code INTEGER NOT NULL,
                    response_body_size INTEGER NOT NULL DEFAULT 0,
                    response_body TEXT,
                    response_headers TEXT,
                    duration_ms INTEGER NOT NULL,
                    was_authenticated INTEGER NOT NULL DEFAULT 0,
                    error_message TEXT,
                    success INTEGER NOT NULL DEFAULT 0
                );

                -- Indexes
                CREATE INDEX IF NOT EXISTS idx_credentials_user_guid ON credentials(user_guid);
                CREATE INDEX IF NOT EXISTS idx_credentials_token_hash ON credentials(bearer_token_hash);
                CREATE INDEX IF NOT EXISTS idx_endpoint_origin_endpoint ON endpoint_origin_mappings(endpoint_identifier);
                CREATE INDEX IF NOT EXISTS idx_endpoint_routes_endpoint ON endpoint_routes(endpoint_identifier);
                CREATE INDEX IF NOT EXISTS idx_url_rewrites_endpoint ON url_rewrites(endpoint_identifier);
                CREATE INDEX IF NOT EXISTS idx_history_timestamp ON request_history(timestamp_utc);
                CREATE INDEX IF NOT EXISTS idx_history_endpoint ON request_history(endpoint_identifier);
                CREATE INDEX IF NOT EXISTS idx_history_status ON request_history(status_code);

                -- Insert initial schema version if not exists
                INSERT OR IGNORE INTO schema_version (version, applied_utc, description)
                VALUES (1, datetime('now'), 'Initial schema');
            ";

            using (SqliteCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            // Schema migration: Add is_read_only column if not exists
            await ExecuteMigrationIfNeededAsync(
                "SELECT COUNT(*) FROM pragma_table_info('credentials') WHERE name='is_read_only'",
                "ALTER TABLE credentials ADD COLUMN is_read_only INTEGER NOT NULL DEFAULT 0",
                token).ConfigureAwait(false);
        }

        private async Task ExecuteMigrationIfNeededAsync(string checkSql, string migrateSql, CancellationToken token)
        {
            using (SqliteCommand checkCmd = _Connection!.CreateCommand())
            {
                checkCmd.CommandText = checkSql;
                object? result = await checkCmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                if (result != null && Convert.ToInt64(result) == 0)
                {
                    using (SqliteCommand migrateCmd = _Connection!.CreateCommand())
                    {
                        migrateCmd.CommandText = migrateSql;
                        await migrateCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <inheritdoc />
        public override async Task<T> InsertAsync<T>(T record, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ArgumentNullException.ThrowIfNull(record);

            string tableName = GetTableName<T>();
            Dictionary<string, object?> values = GetColumnValues(record);

            List<string> columns = values.Keys.ToList();
            List<string> parameters = columns.Select(c => "@" + c).ToList();

            string sql = $"INSERT INTO {tableName} ({String.Join(", ", columns)}) VALUES ({String.Join(", ", parameters)})";

            using (SqliteCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                foreach (KeyValuePair<string, object?> kvp in values)
                {
                    cmd.Parameters.AddWithValue("@" + kvp.Key, kvp.Value ?? DBNull.Value);
                }
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            return record;
        }

        /// <inheritdoc />
        public override async Task<List<T>> SelectAllAsync<T>(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();

            string tableName = GetTableName<T>();
            string sql = $"SELECT * FROM {tableName}";

            List<T> results = new List<T>();

            using (SqliteCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                using (SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        T item = MapReaderToObject<T>(reader);
                        results.Add(item);
                    }
                }
            }

            return results;
        }

        /// <inheritdoc />
        public override async Task<List<T>> SelectAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ArgumentNullException.ThrowIfNull(predicate);

            // For simplicity, we select all and filter in memory
            // A production implementation would translate the expression to SQL
            List<T> all = await SelectAllAsync<T>(token).ConfigureAwait(false);
            Func<T, bool> compiled = predicate.Compile();
            return all.Where(compiled).ToList();
        }

        /// <inheritdoc />
        public override async Task<T?> SelectByIdAsync<T>(string id, CancellationToken token = default) where T : class
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();

            if (String.IsNullOrEmpty(id)) return null;

            string tableName = GetTableName<T>();
            string idColumn = GetIdColumnName<T>();
            string sql = $"SELECT * FROM {tableName} WHERE {idColumn} = @id";

            using (SqliteCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);

                using (SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        return MapReaderToObject<T>(reader);
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public override async Task<T?> SelectByIdAsync<T>(int id, CancellationToken token = default) where T : class
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();

            string tableName = GetTableName<T>();
            string idColumn = GetIdColumnName<T>();
            string sql = $"SELECT * FROM {tableName} WHERE {idColumn} = @id";

            using (SqliteCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);

                using (SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        return MapReaderToObject<T>(reader);
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public override async Task<T?> SelectByGuidAsync<T>(Guid guid, CancellationToken token = default) where T : class
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();

            if (guid == Guid.Empty) return null;

            string tableName = GetTableName<T>();
            string guidColumn = GetGuidColumnName<T>();
            string sql = $"SELECT * FROM {tableName} WHERE {guidColumn} = @guid";

            using (SqliteCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@guid", guid.ToString());

                using (SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        return MapReaderToObject<T>(reader);
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public override async Task<T> UpdateAsync<T>(T record, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ArgumentNullException.ThrowIfNull(record);

            string tableName = GetTableName<T>();
            string idColumn = GetIdColumnName<T>();
            Dictionary<string, object?> values = GetColumnValues(record);
            object? idValue = values[idColumn];
            values.Remove(idColumn);

            List<string> setClauses = values.Keys.Select(c => $"{c} = @{c}").ToList();
            string sql = $"UPDATE {tableName} SET {String.Join(", ", setClauses)} WHERE {idColumn} = @id";

            using (SqliteCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", idValue ?? DBNull.Value);
                foreach (KeyValuePair<string, object?> kvp in values)
                {
                    cmd.Parameters.AddWithValue("@" + kvp.Key, kvp.Value ?? DBNull.Value);
                }
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            return record;
        }

        /// <inheritdoc />
        public override async Task DeleteAsync<T>(T record, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ArgumentNullException.ThrowIfNull(record);

            string tableName = GetTableName<T>();
            string idColumn = GetIdColumnName<T>();
            Dictionary<string, object?> values = GetColumnValues(record);
            object? idValue = values[idColumn];

            string sql = $"DELETE FROM {tableName} WHERE {idColumn} = @id";

            using (SqliteCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", idValue ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public override async Task<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ArgumentNullException.ThrowIfNull(predicate);

            // For simplicity, select matching records and delete them
            List<T> matches = await SelectAsync(predicate, token).ConfigureAwait(false);
            foreach (T item in matches)
            {
                await DeleteAsync(item, token).ConfigureAwait(false);
            }
            return matches.Count;
        }

        /// <inheritdoc />
        public override async Task<bool> ExistsAsync<T>(string id, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();

            if (String.IsNullOrEmpty(id)) return false;

            string tableName = GetTableName<T>();
            string idColumn = GetIdColumnName<T>();
            string sql = $"SELECT COUNT(*) FROM {tableName} WHERE {idColumn} = @id";

            using (SqliteCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);
                object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                return Convert.ToInt64(result) > 0;
            }
        }

        /// <inheritdoc />
        public override async Task<bool> ExistsAsync<T>(int id, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();

            string tableName = GetTableName<T>();
            string idColumn = GetIdColumnName<T>();
            string sql = $"SELECT COUNT(*) FROM {tableName} WHERE {idColumn} = @id";

            using (SqliteCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);
                object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                return Convert.ToInt64(result) > 0;
            }
        }

        /// <inheritdoc />
        public override async Task<bool> ExistsAsync<T>(Guid guid, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();

            if (guid == Guid.Empty) return false;

            string tableName = GetTableName<T>();
            string guidColumn = GetGuidColumnName<T>();
            string sql = $"SELECT COUNT(*) FROM {tableName} WHERE {guidColumn} = @guid";

            using (SqliteCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@guid", guid.ToString());
                object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                return Convert.ToInt64(result) > 0;
            }
        }

        /// <inheritdoc />
        public override async Task<bool> ExistsAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ArgumentNullException.ThrowIfNull(predicate);

            List<T> matches = await SelectAsync(predicate, token).ConfigureAwait(false);
            return matches.Count > 0;
        }

        /// <inheritdoc />
        public override async Task<long> CountAsync<T>(CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();

            string tableName = GetTableName<T>();
            string sql = $"SELECT COUNT(*) FROM {tableName}";

            using (SqliteCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                return Convert.ToInt64(result);
            }
        }

        /// <inheritdoc />
        public override async Task<long> CountAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ArgumentNullException.ThrowIfNull(predicate);

            List<T> matches = await SelectAsync(predicate, token).ConfigureAwait(false);
            return matches.Count;
        }

        #endregion

        #region Protected-Methods

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    _Connection?.Dispose();
                    _Connection = null;
                }
            }
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override async ValueTask DisposeAsyncCore()
        {
            if (_Connection != null)
            {
                await _Connection.DisposeAsync().ConfigureAwait(false);
                _Connection = null;
            }
            await base.DisposeAsyncCore().ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static string GetTableName<T>()
        {
            Type type = typeof(T);
            return type.Name switch
            {
                nameof(UserMaster) => "user_master",
                nameof(Credential) => "credentials",
                nameof(OriginServerConfig) => "origin_servers",
                nameof(ApiEndpointConfig) => "api_endpoints",
                nameof(EndpointOriginMapping) => "endpoint_origin_mappings",
                nameof(EndpointRoute) => "endpoint_routes",
                nameof(UrlRewrite) => "url_rewrites",
                nameof(BlockedHeader) => "blocked_headers",
                nameof(RequestHistory) => "request_history",
                nameof(SchemaVersion) => "schema_version",
                _ => throw new NotSupportedException($"Type {type.Name} is not supported.")
            };
        }

        private static string GetIdColumnName<T>()
        {
            Type type = typeof(T);
            return type.Name switch
            {
                nameof(UserMaster) => "guid",
                nameof(Credential) => "guid",
                nameof(OriginServerConfig) => "identifier",
                nameof(ApiEndpointConfig) => "identifier",
                nameof(EndpointOriginMapping) => "id",
                nameof(EndpointRoute) => "id",
                nameof(UrlRewrite) => "id",
                nameof(BlockedHeader) => "id",
                nameof(RequestHistory) => "request_id",
                nameof(SchemaVersion) => "version",
                _ => throw new NotSupportedException($"Type {type.Name} is not supported.")
            };
        }

        private static string GetGuidColumnName<T>()
        {
            Type type = typeof(T);
            return type.Name switch
            {
                nameof(UserMaster) => "guid",
                nameof(Credential) => "guid",
                nameof(RequestHistory) => "request_id",
                _ => "guid"
            };
        }

        private static Dictionary<string, object?> GetColumnValues<T>(T record)
        {
            Dictionary<string, object?> values = new Dictionary<string, object?>();

            switch (record)
            {
                case UserMaster u:
                    values["guid"] = u.GUID.ToString();
                    values["username"] = u.Username;
                    values["email"] = u.Email;
                    values["first_name"] = u.FirstName;
                    values["last_name"] = u.LastName;
                    values["active"] = u.Active ? 1 : 0;
                    values["is_admin"] = u.IsAdmin ? 1 : 0;
                    values["created_utc"] = u.CreatedUtc.ToString("o");
                    values["modified_utc"] = u.ModifiedUtc?.ToString("o");
                    values["last_login_utc"] = u.LastLoginUtc?.ToString("o");
                    break;

                case Credential c:
                    values["guid"] = c.GUID.ToString();
                    values["user_guid"] = c.UserGUID.ToString();
                    values["name"] = c.Name;
                    values["description"] = c.Description;
                    values["bearer_token_hash"] = c.BearerTokenHash;
                    values["active"] = c.Active ? 1 : 0;
                    values["is_read_only"] = c.IsReadOnly ? 1 : 0;
                    values["created_utc"] = c.CreatedUtc.ToString("o");
                    values["expires_utc"] = c.ExpiresUtc?.ToString("o");
                    values["last_used_utc"] = c.LastUsedUtc?.ToString("o");
                    break;

                case OriginServerConfig o:
                    values["identifier"] = o.Identifier;
                    values["name"] = o.Name;
                    values["hostname"] = o.Hostname;
                    values["port"] = o.Port;
                    values["ssl"] = o.Ssl ? 1 : 0;
                    values["health_check_interval_ms"] = o.HealthCheckIntervalMs;
                    values["health_check_method"] = o.HealthCheckMethod;
                    values["health_check_url"] = o.HealthCheckUrl;
                    values["unhealthy_threshold"] = o.UnhealthyThreshold;
                    values["healthy_threshold"] = o.HealthyThreshold;
                    values["max_parallel_requests"] = o.MaxParallelRequests;
                    values["rate_limit_requests_threshold"] = o.RateLimitRequestsThreshold;
                    values["log_request"] = o.LogRequest ? 1 : 0;
                    values["log_request_body"] = o.LogRequestBody ? 1 : 0;
                    values["log_response"] = o.LogResponse ? 1 : 0;
                    values["log_response_body"] = o.LogResponseBody ? 1 : 0;
                    values["capture_request_body"] = o.CaptureRequestBody ? 1 : 0;
                    values["capture_response_body"] = o.CaptureResponseBody ? 1 : 0;
                    values["capture_request_headers"] = o.CaptureRequestHeaders ? 1 : 0;
                    values["capture_response_headers"] = o.CaptureResponseHeaders ? 1 : 0;
                    values["max_capture_request_body_size"] = o.MaxCaptureRequestBodySize;
                    values["max_capture_response_body_size"] = o.MaxCaptureResponseBodySize;
                    values["created_utc"] = o.CreatedUtc.ToString("o");
                    values["modified_utc"] = o.ModifiedUtc?.ToString("o");
                    break;

                case ApiEndpointConfig e:
                    values["identifier"] = e.Identifier;
                    values["name"] = e.Name;
                    values["timeout_ms"] = e.TimeoutMs;
                    values["load_balancing_mode"] = e.LoadBalancingMode;
                    values["block_http10"] = e.BlockHttp10 ? 1 : 0;
                    values["max_request_body_size"] = e.MaxRequestBodySize;
                    values["log_request_full"] = e.LogRequestFull ? 1 : 0;
                    values["log_request_body"] = e.LogRequestBody ? 1 : 0;
                    values["log_response_body"] = e.LogResponseBody ? 1 : 0;
                    values["include_auth_context_header"] = e.IncludeAuthContextHeader ? 1 : 0;
                    values["auth_context_header"] = e.AuthContextHeader;
                    values["use_global_blocked_headers"] = e.UseGlobalBlockedHeaders ? 1 : 0;
                    values["capture_request_body"] = e.CaptureRequestBody ? 1 : 0;
                    values["capture_response_body"] = e.CaptureResponseBody ? 1 : 0;
                    values["capture_request_headers"] = e.CaptureRequestHeaders ? 1 : 0;
                    values["capture_response_headers"] = e.CaptureResponseHeaders ? 1 : 0;
                    values["max_capture_request_body_size"] = e.MaxCaptureRequestBodySize;
                    values["max_capture_response_body_size"] = e.MaxCaptureResponseBodySize;
                    values["created_utc"] = e.CreatedUtc.ToString("o");
                    values["modified_utc"] = e.ModifiedUtc?.ToString("o");
                    break;

                case EndpointOriginMapping m:
                    if (m.Id > 0) values["id"] = m.Id;
                    values["endpoint_identifier"] = m.EndpointIdentifier;
                    values["origin_identifier"] = m.OriginIdentifier;
                    values["sort_order"] = m.SortOrder;
                    break;

                case EndpointRoute r:
                    if (r.Id > 0) values["id"] = r.Id;
                    values["endpoint_identifier"] = r.EndpointIdentifier;
                    values["http_method"] = r.HttpMethod;
                    values["url_pattern"] = r.UrlPattern;
                    values["requires_authentication"] = r.RequiresAuthentication ? 1 : 0;
                    values["sort_order"] = r.SortOrder;
                    break;

                case UrlRewrite w:
                    if (w.Id > 0) values["id"] = w.Id;
                    values["endpoint_identifier"] = w.EndpointIdentifier;
                    values["http_method"] = w.HttpMethod;
                    values["source_pattern"] = w.SourcePattern;
                    values["target_pattern"] = w.TargetPattern;
                    break;

                case BlockedHeader h:
                    if (h.Id > 0) values["id"] = h.Id;
                    values["header_name"] = h.HeaderName;
                    break;

                case RequestHistory rh:
                    values["request_id"] = rh.RequestId.ToString();
                    values["timestamp_utc"] = rh.TimestampUtc.ToString("o");
                    values["http_method"] = rh.HttpMethod;
                    values["request_path"] = rh.RequestPath;
                    values["query_string"] = rh.QueryString;
                    values["endpoint_identifier"] = rh.EndpointIdentifier;
                    values["origin_identifier"] = rh.OriginIdentifier;
                    values["client_ip"] = rh.ClientIp;
                    values["request_body_size"] = rh.RequestBodySize;
                    values["request_body"] = rh.RequestBody;
                    values["request_headers"] = rh.RequestHeaders;
                    values["status_code"] = rh.StatusCode;
                    values["response_body_size"] = rh.ResponseBodySize;
                    values["response_body"] = rh.ResponseBody;
                    values["response_headers"] = rh.ResponseHeaders;
                    values["duration_ms"] = rh.DurationMs;
                    values["was_authenticated"] = rh.WasAuthenticated ? 1 : 0;
                    values["error_message"] = rh.ErrorMessage;
                    values["success"] = rh.Success ? 1 : 0;
                    break;

                case SchemaVersion sv:
                    values["version"] = sv.Version;
                    values["applied_utc"] = sv.AppliedUtc.ToString("o");
                    values["description"] = sv.Description;
                    break;

                default:
                    throw new NotSupportedException($"Type {typeof(T).Name} is not supported.");
            }

            return values;
        }

        private static T MapReaderToObject<T>(SqliteDataReader reader) where T : class
        {
            Type type = typeof(T);

            if (type == typeof(UserMaster))
            {
                UserMaster u = new UserMaster
                {
                    GUID = Guid.Parse(reader.GetString(reader.GetOrdinal("guid"))),
                    Username = reader.GetString(reader.GetOrdinal("username")),
                    Active = reader.GetInt32(reader.GetOrdinal("active")) == 1,
                    IsAdmin = reader.GetInt32(reader.GetOrdinal("is_admin")) == 1,
                    CreatedUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_utc")))
                };
                if (!reader.IsDBNull(reader.GetOrdinal("email")))
                    u.Email = reader.GetString(reader.GetOrdinal("email"));
                if (!reader.IsDBNull(reader.GetOrdinal("first_name")))
                    u.FirstName = reader.GetString(reader.GetOrdinal("first_name"));
                if (!reader.IsDBNull(reader.GetOrdinal("last_name")))
                    u.LastName = reader.GetString(reader.GetOrdinal("last_name"));
                if (!reader.IsDBNull(reader.GetOrdinal("modified_utc")))
                    u.ModifiedUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("modified_utc")));
                if (!reader.IsDBNull(reader.GetOrdinal("last_login_utc")))
                    u.LastLoginUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("last_login_utc")));
                return (u as T)!;
            }

            if (type == typeof(Credential))
            {
                Credential c = new Credential
                {
                    GUID = Guid.Parse(reader.GetString(reader.GetOrdinal("guid"))),
                    UserGUID = Guid.Parse(reader.GetString(reader.GetOrdinal("user_guid"))),
                    Active = reader.GetInt32(reader.GetOrdinal("active")) == 1,
                    IsReadOnly = reader.GetInt32(reader.GetOrdinal("is_read_only")) == 1,
                    CreatedUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_utc")))
                };
                c.BearerTokenHash = reader.GetString(reader.GetOrdinal("bearer_token_hash"));
                if (!reader.IsDBNull(reader.GetOrdinal("name")))
                    c.Name = reader.GetString(reader.GetOrdinal("name"));
                if (!reader.IsDBNull(reader.GetOrdinal("description")))
                    c.Description = reader.GetString(reader.GetOrdinal("description"));
                if (!reader.IsDBNull(reader.GetOrdinal("expires_utc")))
                    c.ExpiresUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("expires_utc")));
                if (!reader.IsDBNull(reader.GetOrdinal("last_used_utc")))
                    c.LastUsedUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("last_used_utc")));
                return (c as T)!;
            }

            if (type == typeof(OriginServerConfig))
            {
                OriginServerConfig o = new OriginServerConfig
                {
                    Identifier = reader.GetString(reader.GetOrdinal("identifier")),
                    Hostname = reader.GetString(reader.GetOrdinal("hostname")),
                    Port = reader.GetInt32(reader.GetOrdinal("port")),
                    Ssl = reader.GetInt32(reader.GetOrdinal("ssl")) == 1,
                    HealthCheckIntervalMs = reader.GetInt32(reader.GetOrdinal("health_check_interval_ms")),
                    HealthCheckMethod = reader.GetString(reader.GetOrdinal("health_check_method")),
                    HealthCheckUrl = reader.GetString(reader.GetOrdinal("health_check_url")),
                    UnhealthyThreshold = reader.GetInt32(reader.GetOrdinal("unhealthy_threshold")),
                    HealthyThreshold = reader.GetInt32(reader.GetOrdinal("healthy_threshold")),
                    MaxParallelRequests = reader.GetInt32(reader.GetOrdinal("max_parallel_requests")),
                    RateLimitRequestsThreshold = reader.GetInt32(reader.GetOrdinal("rate_limit_requests_threshold")),
                    LogRequest = reader.GetInt32(reader.GetOrdinal("log_request")) == 1,
                    LogRequestBody = reader.GetInt32(reader.GetOrdinal("log_request_body")) == 1,
                    LogResponse = reader.GetInt32(reader.GetOrdinal("log_response")) == 1,
                    LogResponseBody = reader.GetInt32(reader.GetOrdinal("log_response_body")) == 1,
                    CaptureRequestBody = reader.GetInt32(reader.GetOrdinal("capture_request_body")) == 1,
                    CaptureResponseBody = reader.GetInt32(reader.GetOrdinal("capture_response_body")) == 1,
                    CaptureRequestHeaders = reader.GetInt32(reader.GetOrdinal("capture_request_headers")) == 1,
                    CaptureResponseHeaders = reader.GetInt32(reader.GetOrdinal("capture_response_headers")) == 1,
                    MaxCaptureRequestBodySize = reader.GetInt32(reader.GetOrdinal("max_capture_request_body_size")),
                    MaxCaptureResponseBodySize = reader.GetInt32(reader.GetOrdinal("max_capture_response_body_size")),
                    CreatedUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_utc")))
                };
                if (!reader.IsDBNull(reader.GetOrdinal("name")))
                    o.Name = reader.GetString(reader.GetOrdinal("name"));
                if (!reader.IsDBNull(reader.GetOrdinal("modified_utc")))
                    o.ModifiedUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("modified_utc")));
                return (o as T)!;
            }

            if (type == typeof(ApiEndpointConfig))
            {
                ApiEndpointConfig e = new ApiEndpointConfig
                {
                    Identifier = reader.GetString(reader.GetOrdinal("identifier")),
                    TimeoutMs = reader.GetInt32(reader.GetOrdinal("timeout_ms")),
                    LoadBalancingMode = reader.GetString(reader.GetOrdinal("load_balancing_mode")),
                    BlockHttp10 = reader.GetInt32(reader.GetOrdinal("block_http10")) == 1,
                    MaxRequestBodySize = reader.GetInt32(reader.GetOrdinal("max_request_body_size")),
                    LogRequestFull = reader.GetInt32(reader.GetOrdinal("log_request_full")) == 1,
                    LogRequestBody = reader.GetInt32(reader.GetOrdinal("log_request_body")) == 1,
                    LogResponseBody = reader.GetInt32(reader.GetOrdinal("log_response_body")) == 1,
                    IncludeAuthContextHeader = reader.GetInt32(reader.GetOrdinal("include_auth_context_header")) == 1,
                    UseGlobalBlockedHeaders = reader.GetInt32(reader.GetOrdinal("use_global_blocked_headers")) == 1,
                    CaptureRequestBody = reader.GetInt32(reader.GetOrdinal("capture_request_body")) == 1,
                    CaptureResponseBody = reader.GetInt32(reader.GetOrdinal("capture_response_body")) == 1,
                    CaptureRequestHeaders = reader.GetInt32(reader.GetOrdinal("capture_request_headers")) == 1,
                    CaptureResponseHeaders = reader.GetInt32(reader.GetOrdinal("capture_response_headers")) == 1,
                    MaxCaptureRequestBodySize = reader.GetInt32(reader.GetOrdinal("max_capture_request_body_size")),
                    MaxCaptureResponseBodySize = reader.GetInt32(reader.GetOrdinal("max_capture_response_body_size")),
                    CreatedUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_utc")))
                };
                if (!reader.IsDBNull(reader.GetOrdinal("name")))
                    e.Name = reader.GetString(reader.GetOrdinal("name"));
                if (!reader.IsDBNull(reader.GetOrdinal("auth_context_header")))
                    e.AuthContextHeader = reader.GetString(reader.GetOrdinal("auth_context_header"));
                if (!reader.IsDBNull(reader.GetOrdinal("modified_utc")))
                    e.ModifiedUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("modified_utc")));
                return (e as T)!;
            }

            if (type == typeof(EndpointOriginMapping))
            {
                EndpointOriginMapping m = new EndpointOriginMapping
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    EndpointIdentifier = reader.GetString(reader.GetOrdinal("endpoint_identifier")),
                    OriginIdentifier = reader.GetString(reader.GetOrdinal("origin_identifier")),
                    SortOrder = reader.GetInt32(reader.GetOrdinal("sort_order"))
                };
                return (m as T)!;
            }

            if (type == typeof(EndpointRoute))
            {
                EndpointRoute r = new EndpointRoute
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    EndpointIdentifier = reader.GetString(reader.GetOrdinal("endpoint_identifier")),
                    HttpMethod = reader.GetString(reader.GetOrdinal("http_method")),
                    UrlPattern = reader.GetString(reader.GetOrdinal("url_pattern")),
                    RequiresAuthentication = reader.GetInt32(reader.GetOrdinal("requires_authentication")) == 1,
                    SortOrder = reader.GetInt32(reader.GetOrdinal("sort_order"))
                };
                return (r as T)!;
            }

            if (type == typeof(UrlRewrite))
            {
                UrlRewrite w = new UrlRewrite
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    EndpointIdentifier = reader.GetString(reader.GetOrdinal("endpoint_identifier")),
                    HttpMethod = reader.GetString(reader.GetOrdinal("http_method")),
                    SourcePattern = reader.GetString(reader.GetOrdinal("source_pattern")),
                    TargetPattern = reader.GetString(reader.GetOrdinal("target_pattern"))
                };
                return (w as T)!;
            }

            if (type == typeof(BlockedHeader))
            {
                BlockedHeader h = new BlockedHeader
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    HeaderName = reader.GetString(reader.GetOrdinal("header_name"))
                };
                return (h as T)!;
            }

            if (type == typeof(RequestHistory))
            {
                RequestHistory rh = new RequestHistory
                {
                    RequestId = Guid.Parse(reader.GetString(reader.GetOrdinal("request_id"))),
                    TimestampUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("timestamp_utc"))),
                    HttpMethod = reader.GetString(reader.GetOrdinal("http_method")),
                    RequestPath = reader.GetString(reader.GetOrdinal("request_path")),
                    StatusCode = reader.GetInt32(reader.GetOrdinal("status_code")),
                    RequestBodySize = reader.GetInt64(reader.GetOrdinal("request_body_size")),
                    ResponseBodySize = reader.GetInt64(reader.GetOrdinal("response_body_size")),
                    DurationMs = reader.GetInt64(reader.GetOrdinal("duration_ms")),
                    WasAuthenticated = reader.GetInt32(reader.GetOrdinal("was_authenticated")) == 1,
                    Success = reader.GetInt32(reader.GetOrdinal("success")) == 1
                };
                if (!reader.IsDBNull(reader.GetOrdinal("query_string")))
                    rh.QueryString = reader.GetString(reader.GetOrdinal("query_string"));
                if (!reader.IsDBNull(reader.GetOrdinal("endpoint_identifier")))
                    rh.EndpointIdentifier = reader.GetString(reader.GetOrdinal("endpoint_identifier"));
                if (!reader.IsDBNull(reader.GetOrdinal("origin_identifier")))
                    rh.OriginIdentifier = reader.GetString(reader.GetOrdinal("origin_identifier"));
                if (!reader.IsDBNull(reader.GetOrdinal("client_ip")))
                    rh.ClientIp = reader.GetString(reader.GetOrdinal("client_ip"));
                if (!reader.IsDBNull(reader.GetOrdinal("request_body")))
                    rh.RequestBody = reader.GetString(reader.GetOrdinal("request_body"));
                if (!reader.IsDBNull(reader.GetOrdinal("request_headers")))
                    rh.RequestHeaders = reader.GetString(reader.GetOrdinal("request_headers"));
                if (!reader.IsDBNull(reader.GetOrdinal("response_body")))
                    rh.ResponseBody = reader.GetString(reader.GetOrdinal("response_body"));
                if (!reader.IsDBNull(reader.GetOrdinal("response_headers")))
                    rh.ResponseHeaders = reader.GetString(reader.GetOrdinal("response_headers"));
                if (!reader.IsDBNull(reader.GetOrdinal("error_message")))
                    rh.ErrorMessage = reader.GetString(reader.GetOrdinal("error_message"));
                return (rh as T)!;
            }

            if (type == typeof(SchemaVersion))
            {
                SchemaVersion sv = new SchemaVersion
                {
                    Version = reader.GetInt32(reader.GetOrdinal("version")),
                    AppliedUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("applied_utc")))
                };
                if (!reader.IsDBNull(reader.GetOrdinal("description")))
                    sv.Description = reader.GetString(reader.GetOrdinal("description"));
                return (sv as T)!;
            }

            throw new NotSupportedException($"Type {type.Name} is not supported.");
        }

        #endregion
    }
}
