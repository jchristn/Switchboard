#nullable enable

namespace Switchboard.Core.Database.Mysql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MySqlConnector;
    using Switchboard.Core.Models;

    /// <summary>
    /// MySQL database driver implementation.
    /// </summary>
    public class MysqlDatabaseDriver : DatabaseDriverBase
    {
        #region Public-Members

        /// <inheritdoc />
        public override bool IsOpen => _Connection?.State == ConnectionState.Open;

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType => DatabaseTypeEnum.Mysql;

        #endregion

        #region Private-Members

        private MySqlConnection? _Connection = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate MySQL database driver with connection string.
        /// </summary>
        /// <param name="connectionString">MySQL connection string.</param>
        public MysqlDatabaseDriver(string connectionString)
        {
            if (String.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            ConnectionString = connectionString;
        }

        /// <summary>
        /// Instantiate MySQL database driver with individual parameters.
        /// </summary>
        /// <param name="host">Database host.</param>
        /// <param name="port">Database port.</param>
        /// <param name="database">Database name.</param>
        /// <param name="username">Username.</param>
        /// <param name="password">Password.</param>
        /// <param name="sslMode">SSL mode (None, Preferred, Required).</param>
        public MysqlDatabaseDriver(string host, int port, string database, string username, string password, string sslMode = "Preferred")
        {
            if (String.IsNullOrWhiteSpace(host)) throw new ArgumentNullException(nameof(host));
            if (String.IsNullOrWhiteSpace(database)) throw new ArgumentNullException(nameof(database));
            if (String.IsNullOrWhiteSpace(username)) throw new ArgumentNullException(nameof(username));

            MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
            {
                Server = host,
                Port = (uint)port,
                Database = database,
                UserID = username,
                Password = password ?? string.Empty,
                SslMode = Enum.Parse<MySqlSslMode>(sslMode, true)
            };

            ConnectionString = builder.ConnectionString;
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override async Task OpenAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (IsOpen) return;

            _Connection = new MySqlConnection(ConnectionString);
            await _Connection.OpenAsync(token).ConfigureAwait(false);
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
                    version INT PRIMARY KEY,
                    applied_utc DATETIME NOT NULL,
                    description VARCHAR(500)
                );

                -- User master
                CREATE TABLE IF NOT EXISTS user_master (
                    guid VARCHAR(36) PRIMARY KEY,
                    username VARCHAR(255) NOT NULL UNIQUE,
                    email VARCHAR(255),
                    first_name VARCHAR(255),
                    last_name VARCHAR(255),
                    active TINYINT NOT NULL DEFAULT 1,
                    is_admin TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME NOT NULL,
                    modified_utc DATETIME,
                    last_login_utc DATETIME
                );

                -- Credentials (bearer tokens)
                CREATE TABLE IF NOT EXISTS credentials (
                    guid VARCHAR(36) PRIMARY KEY,
                    user_guid VARCHAR(36) NOT NULL,
                    name VARCHAR(255),
                    description TEXT,
                    bearer_token_hash VARCHAR(255) NOT NULL,
                    active TINYINT NOT NULL DEFAULT 1,
                    is_read_only TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME NOT NULL,
                    expires_utc DATETIME,
                    last_used_utc DATETIME,
                    FOREIGN KEY (user_guid) REFERENCES user_master(guid) ON DELETE CASCADE
                );

                -- Origin servers
                CREATE TABLE IF NOT EXISTS origin_servers (
                    identifier VARCHAR(255) PRIMARY KEY,
                    name VARCHAR(255),
                    hostname VARCHAR(255) NOT NULL,
                    port INT NOT NULL,
                    ssl TINYINT NOT NULL DEFAULT 0,
                    health_check_interval_ms INT NOT NULL DEFAULT 5000,
                    health_check_method VARCHAR(10) NOT NULL DEFAULT 'HEAD',
                    health_check_url VARCHAR(1000) NOT NULL DEFAULT '/',
                    unhealthy_threshold INT NOT NULL DEFAULT 2,
                    healthy_threshold INT NOT NULL DEFAULT 1,
                    max_parallel_requests INT NOT NULL DEFAULT 10,
                    rate_limit_requests_threshold INT NOT NULL DEFAULT 30,
                    log_request TINYINT NOT NULL DEFAULT 0,
                    log_request_body TINYINT NOT NULL DEFAULT 0,
                    log_response TINYINT NOT NULL DEFAULT 0,
                    log_response_body TINYINT NOT NULL DEFAULT 0,
                    capture_request_body TINYINT NOT NULL DEFAULT 0,
                    capture_response_body TINYINT NOT NULL DEFAULT 0,
                    capture_request_headers TINYINT NOT NULL DEFAULT 1,
                    capture_response_headers TINYINT NOT NULL DEFAULT 1,
                    max_capture_request_body_size INT NOT NULL DEFAULT 65536,
                    max_capture_response_body_size INT NOT NULL DEFAULT 65536,
                    created_utc DATETIME NOT NULL,
                    modified_utc DATETIME
                );

                -- API endpoints
                CREATE TABLE IF NOT EXISTS api_endpoints (
                    identifier VARCHAR(255) PRIMARY KEY,
                    name VARCHAR(255),
                    timeout_ms INT NOT NULL DEFAULT 60000,
                    load_balancing_mode VARCHAR(50) NOT NULL DEFAULT 'RoundRobin',
                    block_http10 TINYINT NOT NULL DEFAULT 0,
                    max_request_body_size BIGINT NOT NULL DEFAULT 536870912,
                    log_request_full TINYINT NOT NULL DEFAULT 0,
                    log_request_body TINYINT NOT NULL DEFAULT 0,
                    log_response_body TINYINT NOT NULL DEFAULT 0,
                    include_auth_context_header TINYINT NOT NULL DEFAULT 1,
                    auth_context_header VARCHAR(255) DEFAULT 'x-sb-auth-context',
                    use_global_blocked_headers TINYINT NOT NULL DEFAULT 1,
                    capture_request_body TINYINT NOT NULL DEFAULT 0,
                    capture_response_body TINYINT NOT NULL DEFAULT 0,
                    capture_request_headers TINYINT NOT NULL DEFAULT 1,
                    capture_response_headers TINYINT NOT NULL DEFAULT 1,
                    max_capture_request_body_size INT NOT NULL DEFAULT 65536,
                    max_capture_response_body_size INT NOT NULL DEFAULT 65536,
                    created_utc DATETIME NOT NULL,
                    modified_utc DATETIME
                );

                -- Endpoint-origin mappings
                CREATE TABLE IF NOT EXISTS endpoint_origin_mappings (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    endpoint_identifier VARCHAR(255) NOT NULL,
                    origin_identifier VARCHAR(255) NOT NULL,
                    sort_order INT NOT NULL DEFAULT 0,
                    FOREIGN KEY (endpoint_identifier) REFERENCES api_endpoints(identifier) ON DELETE CASCADE,
                    FOREIGN KEY (origin_identifier) REFERENCES origin_servers(identifier) ON DELETE CASCADE,
                    UNIQUE KEY unique_mapping (endpoint_identifier, origin_identifier)
                );

                -- Endpoint routes
                CREATE TABLE IF NOT EXISTS endpoint_routes (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    endpoint_identifier VARCHAR(255) NOT NULL,
                    http_method VARCHAR(10) NOT NULL,
                    url_pattern VARCHAR(1000) NOT NULL,
                    requires_authentication TINYINT NOT NULL DEFAULT 0,
                    sort_order INT NOT NULL DEFAULT 0,
                    FOREIGN KEY (endpoint_identifier) REFERENCES api_endpoints(identifier) ON DELETE CASCADE
                );

                -- URL rewrite rules
                CREATE TABLE IF NOT EXISTS url_rewrites (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    endpoint_identifier VARCHAR(255) NOT NULL,
                    http_method VARCHAR(10) NOT NULL,
                    source_pattern VARCHAR(1000) NOT NULL,
                    target_pattern VARCHAR(1000) NOT NULL,
                    FOREIGN KEY (endpoint_identifier) REFERENCES api_endpoints(identifier) ON DELETE CASCADE
                );

                -- Blocked headers
                CREATE TABLE IF NOT EXISTS blocked_headers (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    header_name VARCHAR(255) NOT NULL UNIQUE
                );

                -- Request history
                CREATE TABLE IF NOT EXISTS request_history (
                    request_id VARCHAR(36) PRIMARY KEY,
                    timestamp_utc DATETIME(6) NOT NULL,
                    http_method VARCHAR(10) NOT NULL,
                    request_path VARCHAR(2000) NOT NULL,
                    query_string TEXT,
                    endpoint_identifier VARCHAR(255),
                    origin_identifier VARCHAR(255),
                    client_ip VARCHAR(45),
                    request_body_size BIGINT NOT NULL DEFAULT 0,
                    request_body LONGTEXT,
                    request_headers TEXT,
                    status_code INT NOT NULL,
                    response_body_size BIGINT NOT NULL DEFAULT 0,
                    response_body LONGTEXT,
                    response_headers TEXT,
                    duration_ms BIGINT NOT NULL,
                    was_authenticated TINYINT NOT NULL DEFAULT 0,
                    error_message TEXT,
                    success TINYINT NOT NULL DEFAULT 0,
                    INDEX idx_history_timestamp (timestamp_utc),
                    INDEX idx_history_endpoint (endpoint_identifier),
                    INDEX idx_history_status (status_code)
                );

                -- Indexes
                CREATE INDEX IF NOT EXISTS idx_credentials_user_guid ON credentials(user_guid);
                CREATE INDEX IF NOT EXISTS idx_credentials_token_hash ON credentials(bearer_token_hash);
                CREATE INDEX IF NOT EXISTS idx_endpoint_origin_endpoint ON endpoint_origin_mappings(endpoint_identifier);
                CREATE INDEX IF NOT EXISTS idx_endpoint_routes_endpoint ON endpoint_routes(endpoint_identifier);
                CREATE INDEX IF NOT EXISTS idx_url_rewrites_endpoint ON url_rewrites(endpoint_identifier);
            ";

            // MySQL doesn't support CREATE INDEX IF NOT EXISTS in all versions, so we ignore errors
            string[] statements = sql.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string statement in statements)
            {
                string trimmed = statement.Trim();
                if (String.IsNullOrEmpty(trimmed)) continue;

                try
                {
                    using (MySqlCommand cmd = _Connection!.CreateCommand())
                    {
                        cmd.CommandText = trimmed;
                        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }
                }
                catch (MySqlException ex) when (ex.Number == 1061) // Duplicate key name
                {
                    // Index already exists, ignore
                }
            }

            // Insert initial schema version
            try
            {
                using (MySqlCommand cmd = _Connection!.CreateCommand())
                {
                    cmd.CommandText = "INSERT IGNORE INTO schema_version (version, applied_utc, description) VALUES (1, NOW(), 'Initial schema')";
                    await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
            catch
            {
                // Ignore if already exists
            }

            // Schema migration: Add is_read_only column if not exists
            await AddColumnIfNotExistsAsync("credentials", "is_read_only", "TINYINT NOT NULL DEFAULT 0", token).ConfigureAwait(false);
        }

        private async Task AddColumnIfNotExistsAsync(string tableName, string columnName, string columnDefinition, CancellationToken token)
        {
            try
            {
                using (MySqlCommand checkCmd = _Connection!.CreateCommand())
                {
                    checkCmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{columnName}'";
                    object? result = await checkCmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                    if (result != null && Convert.ToInt64(result) == 0)
                    {
                        using (MySqlCommand alterCmd = _Connection!.CreateCommand())
                        {
                            alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
                            await alterCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors, column may already exist
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

            using (MySqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                foreach (KeyValuePair<string, object?> kvp in values)
                {
                    cmd.Parameters.AddWithValue("@" + kvp.Key, kvp.Value ?? DBNull.Value);
                }
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                // Get last insert ID for auto-increment fields
                if (record is EndpointOriginMapping mapping && mapping.Id == 0)
                {
                    mapping.Id = (int)cmd.LastInsertedId;
                }
                else if (record is EndpointRoute route && route.Id == 0)
                {
                    route.Id = (int)cmd.LastInsertedId;
                }
                else if (record is UrlRewrite rewrite && rewrite.Id == 0)
                {
                    rewrite.Id = (int)cmd.LastInsertedId;
                }
                else if (record is BlockedHeader header && header.Id == 0)
                {
                    header.Id = (int)cmd.LastInsertedId;
                }
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

            using (MySqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                using (MySqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (MySqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);

                using (MySqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (MySqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);

                using (MySqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (MySqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@guid", guid.ToString());

                using (MySqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (MySqlCommand cmd = _Connection!.CreateCommand())
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

            using (MySqlCommand cmd = _Connection!.CreateCommand())
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

            using (MySqlCommand cmd = _Connection!.CreateCommand())
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

            using (MySqlCommand cmd = _Connection!.CreateCommand())
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

            using (MySqlCommand cmd = _Connection!.CreateCommand())
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

            using (MySqlCommand cmd = _Connection!.CreateCommand())
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
                    values["created_utc"] = u.CreatedUtc;
                    values["modified_utc"] = u.ModifiedUtc;
                    values["last_login_utc"] = u.LastLoginUtc;
                    break;

                case Credential c:
                    values["guid"] = c.GUID.ToString();
                    values["user_guid"] = c.UserGUID.ToString();
                    values["name"] = c.Name;
                    values["description"] = c.Description;
                    values["bearer_token_hash"] = c.BearerTokenHash;
                    values["active"] = c.Active ? 1 : 0;
                    values["is_read_only"] = c.IsReadOnly ? 1 : 0;
                    values["created_utc"] = c.CreatedUtc;
                    values["expires_utc"] = c.ExpiresUtc;
                    values["last_used_utc"] = c.LastUsedUtc;
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
                    values["created_utc"] = o.CreatedUtc;
                    values["modified_utc"] = o.ModifiedUtc;
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
                    values["created_utc"] = e.CreatedUtc;
                    values["modified_utc"] = e.ModifiedUtc;
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
                    values["timestamp_utc"] = rh.TimestampUtc;
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
                    values["applied_utc"] = sv.AppliedUtc;
                    values["description"] = sv.Description;
                    break;

                default:
                    throw new NotSupportedException($"Type {typeof(T).Name} is not supported.");
            }

            return values;
        }

        private static T MapReaderToObject<T>(MySqlDataReader reader) where T : class
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
                    CreatedUtc = reader.GetDateTime(reader.GetOrdinal("created_utc"))
                };
                if (!reader.IsDBNull(reader.GetOrdinal("email")))
                    u.Email = reader.GetString(reader.GetOrdinal("email"));
                if (!reader.IsDBNull(reader.GetOrdinal("first_name")))
                    u.FirstName = reader.GetString(reader.GetOrdinal("first_name"));
                if (!reader.IsDBNull(reader.GetOrdinal("last_name")))
                    u.LastName = reader.GetString(reader.GetOrdinal("last_name"));
                if (!reader.IsDBNull(reader.GetOrdinal("modified_utc")))
                    u.ModifiedUtc = reader.GetDateTime(reader.GetOrdinal("modified_utc"));
                if (!reader.IsDBNull(reader.GetOrdinal("last_login_utc")))
                    u.LastLoginUtc = reader.GetDateTime(reader.GetOrdinal("last_login_utc"));
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
                    CreatedUtc = reader.GetDateTime(reader.GetOrdinal("created_utc"))
                };
                c.BearerTokenHash = reader.GetString(reader.GetOrdinal("bearer_token_hash"));
                if (!reader.IsDBNull(reader.GetOrdinal("name")))
                    c.Name = reader.GetString(reader.GetOrdinal("name"));
                if (!reader.IsDBNull(reader.GetOrdinal("description")))
                    c.Description = reader.GetString(reader.GetOrdinal("description"));
                if (!reader.IsDBNull(reader.GetOrdinal("expires_utc")))
                    c.ExpiresUtc = reader.GetDateTime(reader.GetOrdinal("expires_utc"));
                if (!reader.IsDBNull(reader.GetOrdinal("last_used_utc")))
                    c.LastUsedUtc = reader.GetDateTime(reader.GetOrdinal("last_used_utc"));
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
                    CreatedUtc = reader.GetDateTime(reader.GetOrdinal("created_utc"))
                };
                if (!reader.IsDBNull(reader.GetOrdinal("name")))
                    o.Name = reader.GetString(reader.GetOrdinal("name"));
                if (!reader.IsDBNull(reader.GetOrdinal("modified_utc")))
                    o.ModifiedUtc = reader.GetDateTime(reader.GetOrdinal("modified_utc"));
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
                    CreatedUtc = reader.GetDateTime(reader.GetOrdinal("created_utc"))
                };
                if (!reader.IsDBNull(reader.GetOrdinal("name")))
                    e.Name = reader.GetString(reader.GetOrdinal("name"));
                if (!reader.IsDBNull(reader.GetOrdinal("auth_context_header")))
                    e.AuthContextHeader = reader.GetString(reader.GetOrdinal("auth_context_header"));
                if (!reader.IsDBNull(reader.GetOrdinal("modified_utc")))
                    e.ModifiedUtc = reader.GetDateTime(reader.GetOrdinal("modified_utc"));
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
                    TimestampUtc = reader.GetDateTime(reader.GetOrdinal("timestamp_utc")),
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
                    AppliedUtc = reader.GetDateTime(reader.GetOrdinal("applied_utc"))
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
