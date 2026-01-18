#nullable enable

namespace Switchboard.Core.Database.Postgres
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using Switchboard.Core.Models;

    /// <summary>
    /// PostgreSQL database driver implementation.
    /// </summary>
    public class PostgresDatabaseDriver : DatabaseDriverBase
    {
        #region Public-Members

        /// <inheritdoc />
        public override bool IsOpen => _Connection?.State == ConnectionState.Open;

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType => DatabaseTypeEnum.Postgres;

        #endregion

        #region Private-Members

        private NpgsqlConnection? _Connection = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate PostgreSQL database driver with connection string.
        /// </summary>
        /// <param name="connectionString">PostgreSQL connection string.</param>
        public PostgresDatabaseDriver(string connectionString)
        {
            if (String.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            ConnectionString = connectionString;
        }

        /// <summary>
        /// Instantiate PostgreSQL database driver with individual parameters.
        /// </summary>
        /// <param name="host">Database host.</param>
        /// <param name="port">Database port.</param>
        /// <param name="database">Database name.</param>
        /// <param name="username">Username.</param>
        /// <param name="password">Password.</param>
        /// <param name="sslMode">SSL mode (Disable, Prefer, Require).</param>
        public PostgresDatabaseDriver(string host, int port, string database, string username, string password, string sslMode = "Prefer")
        {
            if (String.IsNullOrWhiteSpace(host)) throw new ArgumentNullException(nameof(host));
            if (String.IsNullOrWhiteSpace(database)) throw new ArgumentNullException(nameof(database));
            if (String.IsNullOrWhiteSpace(username)) throw new ArgumentNullException(nameof(username));

            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = database,
                Username = username,
                Password = password ?? string.Empty,
                SslMode = Enum.Parse<SslMode>(sslMode, true)
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

            _Connection = new NpgsqlConnection(ConnectionString);
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
                    version INTEGER PRIMARY KEY,
                    applied_utc TIMESTAMP NOT NULL,
                    description VARCHAR(500)
                );

                -- User master
                CREATE TABLE IF NOT EXISTS user_master (
                    guid VARCHAR(36) PRIMARY KEY,
                    username VARCHAR(255) NOT NULL UNIQUE,
                    email VARCHAR(255),
                    first_name VARCHAR(255),
                    last_name VARCHAR(255),
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    is_admin BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL,
                    modified_utc TIMESTAMP,
                    last_login_utc TIMESTAMP
                );

                -- Credentials (bearer tokens)
                CREATE TABLE IF NOT EXISTS credentials (
                    guid VARCHAR(36) PRIMARY KEY,
                    user_guid VARCHAR(36) NOT NULL REFERENCES user_master(guid) ON DELETE CASCADE,
                    name VARCHAR(255),
                    description TEXT,
                    bearer_token_hash VARCHAR(255) NOT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    is_read_only BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL,
                    expires_utc TIMESTAMP,
                    last_used_utc TIMESTAMP
                );

                -- Origin servers
                CREATE TABLE IF NOT EXISTS origin_servers (
                    identifier VARCHAR(255) PRIMARY KEY,
                    name VARCHAR(255),
                    hostname VARCHAR(255) NOT NULL,
                    port INTEGER NOT NULL,
                    ssl BOOLEAN NOT NULL DEFAULT FALSE,
                    health_check_interval_ms INTEGER NOT NULL DEFAULT 5000,
                    health_check_method VARCHAR(10) NOT NULL DEFAULT 'HEAD',
                    health_check_url VARCHAR(1000) NOT NULL DEFAULT '/',
                    unhealthy_threshold INTEGER NOT NULL DEFAULT 2,
                    healthy_threshold INTEGER NOT NULL DEFAULT 1,
                    max_parallel_requests INTEGER NOT NULL DEFAULT 10,
                    rate_limit_requests_threshold INTEGER NOT NULL DEFAULT 30,
                    log_request BOOLEAN NOT NULL DEFAULT FALSE,
                    log_request_body BOOLEAN NOT NULL DEFAULT FALSE,
                    log_response BOOLEAN NOT NULL DEFAULT FALSE,
                    log_response_body BOOLEAN NOT NULL DEFAULT FALSE,
                    capture_request_body BOOLEAN NOT NULL DEFAULT FALSE,
                    capture_response_body BOOLEAN NOT NULL DEFAULT FALSE,
                    capture_request_headers BOOLEAN NOT NULL DEFAULT TRUE,
                    capture_response_headers BOOLEAN NOT NULL DEFAULT TRUE,
                    max_capture_request_body_size INTEGER NOT NULL DEFAULT 65536,
                    max_capture_response_body_size INTEGER NOT NULL DEFAULT 65536,
                    created_utc TIMESTAMP NOT NULL,
                    modified_utc TIMESTAMP
                );

                -- API endpoints
                CREATE TABLE IF NOT EXISTS api_endpoints (
                    identifier VARCHAR(255) PRIMARY KEY,
                    name VARCHAR(255),
                    timeout_ms INTEGER NOT NULL DEFAULT 60000,
                    load_balancing_mode VARCHAR(50) NOT NULL DEFAULT 'RoundRobin',
                    block_http10 BOOLEAN NOT NULL DEFAULT FALSE,
                    max_request_body_size BIGINT NOT NULL DEFAULT 536870912,
                    log_request_full BOOLEAN NOT NULL DEFAULT FALSE,
                    log_request_body BOOLEAN NOT NULL DEFAULT FALSE,
                    log_response_body BOOLEAN NOT NULL DEFAULT FALSE,
                    include_auth_context_header BOOLEAN NOT NULL DEFAULT TRUE,
                    auth_context_header VARCHAR(255) DEFAULT 'x-sb-auth-context',
                    use_global_blocked_headers BOOLEAN NOT NULL DEFAULT TRUE,
                    capture_request_body BOOLEAN NOT NULL DEFAULT FALSE,
                    capture_response_body BOOLEAN NOT NULL DEFAULT FALSE,
                    capture_request_headers BOOLEAN NOT NULL DEFAULT TRUE,
                    capture_response_headers BOOLEAN NOT NULL DEFAULT TRUE,
                    max_capture_request_body_size INTEGER NOT NULL DEFAULT 65536,
                    max_capture_response_body_size INTEGER NOT NULL DEFAULT 65536,
                    created_utc TIMESTAMP NOT NULL,
                    modified_utc TIMESTAMP
                );

                -- Endpoint-origin mappings
                CREATE TABLE IF NOT EXISTS endpoint_origin_mappings (
                    id SERIAL PRIMARY KEY,
                    endpoint_identifier VARCHAR(255) NOT NULL REFERENCES api_endpoints(identifier) ON DELETE CASCADE,
                    origin_identifier VARCHAR(255) NOT NULL REFERENCES origin_servers(identifier) ON DELETE CASCADE,
                    sort_order INTEGER NOT NULL DEFAULT 0,
                    UNIQUE (endpoint_identifier, origin_identifier)
                );

                -- Endpoint routes
                CREATE TABLE IF NOT EXISTS endpoint_routes (
                    id SERIAL PRIMARY KEY,
                    endpoint_identifier VARCHAR(255) NOT NULL REFERENCES api_endpoints(identifier) ON DELETE CASCADE,
                    http_method VARCHAR(10) NOT NULL,
                    url_pattern VARCHAR(1000) NOT NULL,
                    requires_authentication BOOLEAN NOT NULL DEFAULT FALSE,
                    sort_order INTEGER NOT NULL DEFAULT 0
                );

                -- URL rewrite rules
                CREATE TABLE IF NOT EXISTS url_rewrites (
                    id SERIAL PRIMARY KEY,
                    endpoint_identifier VARCHAR(255) NOT NULL REFERENCES api_endpoints(identifier) ON DELETE CASCADE,
                    http_method VARCHAR(10) NOT NULL,
                    source_pattern VARCHAR(1000) NOT NULL,
                    target_pattern VARCHAR(1000) NOT NULL
                );

                -- Blocked headers
                CREATE TABLE IF NOT EXISTS blocked_headers (
                    id SERIAL PRIMARY KEY,
                    header_name VARCHAR(255) NOT NULL UNIQUE
                );

                -- Request history
                CREATE TABLE IF NOT EXISTS request_history (
                    request_id VARCHAR(36) PRIMARY KEY,
                    timestamp_utc TIMESTAMP(6) NOT NULL,
                    http_method VARCHAR(10) NOT NULL,
                    request_path VARCHAR(2000) NOT NULL,
                    query_string TEXT,
                    endpoint_identifier VARCHAR(255),
                    origin_identifier VARCHAR(255),
                    client_ip VARCHAR(45),
                    request_body_size BIGINT NOT NULL DEFAULT 0,
                    request_body TEXT,
                    request_headers TEXT,
                    status_code INTEGER NOT NULL,
                    response_body_size BIGINT NOT NULL DEFAULT 0,
                    response_body TEXT,
                    response_headers TEXT,
                    duration_ms BIGINT NOT NULL,
                    was_authenticated BOOLEAN NOT NULL DEFAULT FALSE,
                    error_message TEXT,
                    success BOOLEAN NOT NULL DEFAULT FALSE
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
            ";

            string[] statements = sql.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string statement in statements)
            {
                string trimmed = statement.Trim();
                if (String.IsNullOrEmpty(trimmed)) continue;

                using (NpgsqlCommand cmd = _Connection!.CreateCommand())
                {
                    cmd.CommandText = trimmed;
                    await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }

            // Insert initial schema version
            try
            {
                using (NpgsqlCommand cmd = _Connection!.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO schema_version (version, applied_utc, description) VALUES (1, NOW(), 'Initial schema') ON CONFLICT DO NOTHING";
                    await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
            catch
            {
                // Ignore if already exists
            }

            // Schema migration: Add is_read_only column if not exists
            await AddColumnIfNotExistsAsync("credentials", "is_read_only", "BOOLEAN NOT NULL DEFAULT FALSE", token).ConfigureAwait(false);
        }

        private async Task AddColumnIfNotExistsAsync(string tableName, string columnName, string columnDefinition, CancellationToken token)
        {
            try
            {
                using (NpgsqlCommand checkCmd = _Connection!.CreateCommand())
                {
                    checkCmd.CommandText = $"SELECT COUNT(*) FROM information_schema.columns WHERE table_name = '{tableName}' AND column_name = '{columnName}'";
                    object? result = await checkCmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                    if (result != null && Convert.ToInt64(result) == 0)
                    {
                        using (NpgsqlCommand alterCmd = _Connection!.CreateCommand())
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

            // Handle SERIAL columns for auto-increment
            bool hasAutoIncrement = record is EndpointOriginMapping or EndpointRoute or UrlRewrite or BlockedHeader;
            string returningClause = hasAutoIncrement ? " RETURNING id" : "";

            string sql = $"INSERT INTO {tableName} ({String.Join(", ", columns)}) VALUES ({String.Join(", ", parameters)}){returningClause}";

            using (NpgsqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                foreach (KeyValuePair<string, object?> kvp in values)
                {
                    cmd.Parameters.AddWithValue("@" + kvp.Key, kvp.Value ?? DBNull.Value);
                }

                if (hasAutoIncrement)
                {
                    object? result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                    int newId = Convert.ToInt32(result);

                    if (record is EndpointOriginMapping mapping)
                        mapping.Id = newId;
                    else if (record is EndpointRoute route)
                        route.Id = newId;
                    else if (record is UrlRewrite rewrite)
                        rewrite.Id = newId;
                    else if (record is BlockedHeader header)
                        header.Id = newId;
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
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

            using (NpgsqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (NpgsqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);

                using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (NpgsqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);

                using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (NpgsqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@guid", guid.ToString());

                using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (NpgsqlCommand cmd = _Connection!.CreateCommand())
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

            using (NpgsqlCommand cmd = _Connection!.CreateCommand())
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

            using (NpgsqlCommand cmd = _Connection!.CreateCommand())
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

            using (NpgsqlCommand cmd = _Connection!.CreateCommand())
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

            using (NpgsqlCommand cmd = _Connection!.CreateCommand())
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

            using (NpgsqlCommand cmd = _Connection!.CreateCommand())
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
                    values["active"] = u.Active;
                    values["is_admin"] = u.IsAdmin;
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
                    values["active"] = c.Active;
                    values["is_read_only"] = c.IsReadOnly;
                    values["created_utc"] = c.CreatedUtc;
                    values["expires_utc"] = c.ExpiresUtc;
                    values["last_used_utc"] = c.LastUsedUtc;
                    break;

                case OriginServerConfig o:
                    values["identifier"] = o.Identifier;
                    values["name"] = o.Name;
                    values["hostname"] = o.Hostname;
                    values["port"] = o.Port;
                    values["ssl"] = o.Ssl;
                    values["health_check_interval_ms"] = o.HealthCheckIntervalMs;
                    values["health_check_method"] = o.HealthCheckMethod;
                    values["health_check_url"] = o.HealthCheckUrl;
                    values["unhealthy_threshold"] = o.UnhealthyThreshold;
                    values["healthy_threshold"] = o.HealthyThreshold;
                    values["max_parallel_requests"] = o.MaxParallelRequests;
                    values["rate_limit_requests_threshold"] = o.RateLimitRequestsThreshold;
                    values["log_request"] = o.LogRequest;
                    values["log_request_body"] = o.LogRequestBody;
                    values["log_response"] = o.LogResponse;
                    values["log_response_body"] = o.LogResponseBody;
                    values["capture_request_body"] = o.CaptureRequestBody;
                    values["capture_response_body"] = o.CaptureResponseBody;
                    values["capture_request_headers"] = o.CaptureRequestHeaders;
                    values["capture_response_headers"] = o.CaptureResponseHeaders;
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
                    values["block_http10"] = e.BlockHttp10;
                    values["max_request_body_size"] = e.MaxRequestBodySize;
                    values["log_request_full"] = e.LogRequestFull;
                    values["log_request_body"] = e.LogRequestBody;
                    values["log_response_body"] = e.LogResponseBody;
                    values["include_auth_context_header"] = e.IncludeAuthContextHeader;
                    values["auth_context_header"] = e.AuthContextHeader;
                    values["use_global_blocked_headers"] = e.UseGlobalBlockedHeaders;
                    values["capture_request_body"] = e.CaptureRequestBody;
                    values["capture_response_body"] = e.CaptureResponseBody;
                    values["capture_request_headers"] = e.CaptureRequestHeaders;
                    values["capture_response_headers"] = e.CaptureResponseHeaders;
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
                    values["requires_authentication"] = r.RequiresAuthentication;
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
                    values["was_authenticated"] = rh.WasAuthenticated;
                    values["error_message"] = rh.ErrorMessage;
                    values["success"] = rh.Success;
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

        private static T MapReaderToObject<T>(NpgsqlDataReader reader) where T : class
        {
            Type type = typeof(T);

            if (type == typeof(UserMaster))
            {
                UserMaster u = new UserMaster
                {
                    GUID = Guid.Parse(reader.GetString(reader.GetOrdinal("guid"))),
                    Username = reader.GetString(reader.GetOrdinal("username")),
                    Active = reader.GetBoolean(reader.GetOrdinal("active")),
                    IsAdmin = reader.GetBoolean(reader.GetOrdinal("is_admin")),
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
                    Active = reader.GetBoolean(reader.GetOrdinal("active")),
                    IsReadOnly = reader.GetBoolean(reader.GetOrdinal("is_read_only")),
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
                    Ssl = reader.GetBoolean(reader.GetOrdinal("ssl")),
                    HealthCheckIntervalMs = reader.GetInt32(reader.GetOrdinal("health_check_interval_ms")),
                    HealthCheckMethod = reader.GetString(reader.GetOrdinal("health_check_method")),
                    HealthCheckUrl = reader.GetString(reader.GetOrdinal("health_check_url")),
                    UnhealthyThreshold = reader.GetInt32(reader.GetOrdinal("unhealthy_threshold")),
                    HealthyThreshold = reader.GetInt32(reader.GetOrdinal("healthy_threshold")),
                    MaxParallelRequests = reader.GetInt32(reader.GetOrdinal("max_parallel_requests")),
                    RateLimitRequestsThreshold = reader.GetInt32(reader.GetOrdinal("rate_limit_requests_threshold")),
                    LogRequest = reader.GetBoolean(reader.GetOrdinal("log_request")),
                    LogRequestBody = reader.GetBoolean(reader.GetOrdinal("log_request_body")),
                    LogResponse = reader.GetBoolean(reader.GetOrdinal("log_response")),
                    LogResponseBody = reader.GetBoolean(reader.GetOrdinal("log_response_body")),
                    CaptureRequestBody = reader.GetBoolean(reader.GetOrdinal("capture_request_body")),
                    CaptureResponseBody = reader.GetBoolean(reader.GetOrdinal("capture_response_body")),
                    CaptureRequestHeaders = reader.GetBoolean(reader.GetOrdinal("capture_request_headers")),
                    CaptureResponseHeaders = reader.GetBoolean(reader.GetOrdinal("capture_response_headers")),
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
                    BlockHttp10 = reader.GetBoolean(reader.GetOrdinal("block_http10")),
                    MaxRequestBodySize = reader.GetInt32(reader.GetOrdinal("max_request_body_size")),
                    LogRequestFull = reader.GetBoolean(reader.GetOrdinal("log_request_full")),
                    LogRequestBody = reader.GetBoolean(reader.GetOrdinal("log_request_body")),
                    LogResponseBody = reader.GetBoolean(reader.GetOrdinal("log_response_body")),
                    IncludeAuthContextHeader = reader.GetBoolean(reader.GetOrdinal("include_auth_context_header")),
                    UseGlobalBlockedHeaders = reader.GetBoolean(reader.GetOrdinal("use_global_blocked_headers")),
                    CaptureRequestBody = reader.GetBoolean(reader.GetOrdinal("capture_request_body")),
                    CaptureResponseBody = reader.GetBoolean(reader.GetOrdinal("capture_response_body")),
                    CaptureRequestHeaders = reader.GetBoolean(reader.GetOrdinal("capture_request_headers")),
                    CaptureResponseHeaders = reader.GetBoolean(reader.GetOrdinal("capture_response_headers")),
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
                    RequiresAuthentication = reader.GetBoolean(reader.GetOrdinal("requires_authentication")),
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
                    WasAuthenticated = reader.GetBoolean(reader.GetOrdinal("was_authenticated")),
                    Success = reader.GetBoolean(reader.GetOrdinal("success"))
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
