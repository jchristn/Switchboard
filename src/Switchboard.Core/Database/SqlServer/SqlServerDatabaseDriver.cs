#nullable enable

namespace Switchboard.Core.Database.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Switchboard.Core.Models;

    /// <summary>
    /// SQL Server database driver implementation.
    /// </summary>
    public class SqlServerDatabaseDriver : DatabaseDriverBase
    {
        #region Public-Members

        /// <inheritdoc />
        public override bool IsOpen => _Connection?.State == ConnectionState.Open;

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType => DatabaseTypeEnum.SqlServer;

        #endregion

        #region Private-Members

        private SqlConnection? _Connection = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate SQL Server database driver with connection string.
        /// </summary>
        /// <param name="connectionString">SQL Server connection string.</param>
        public SqlServerDatabaseDriver(string connectionString)
        {
            if (String.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            ConnectionString = connectionString;
        }

        /// <summary>
        /// Instantiate SQL Server database driver with individual parameters.
        /// </summary>
        /// <param name="host">Database host.</param>
        /// <param name="port">Database port (default 1433).</param>
        /// <param name="database">Database name.</param>
        /// <param name="username">Username (null for Windows auth).</param>
        /// <param name="password">Password.</param>
        /// <param name="trustServerCertificate">Trust server certificate.</param>
        public SqlServerDatabaseDriver(string host, int port, string database, string? username, string? password, bool trustServerCertificate = false)
        {
            if (String.IsNullOrWhiteSpace(host)) throw new ArgumentNullException(nameof(host));
            if (String.IsNullOrWhiteSpace(database)) throw new ArgumentNullException(nameof(database));

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
            {
                DataSource = port == 1433 ? host : $"{host},{port}",
                InitialCatalog = database,
                TrustServerCertificate = trustServerCertificate
            };

            if (String.IsNullOrEmpty(username))
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = username;
                builder.Password = password ?? string.Empty;
            }

            ConnectionString = builder.ConnectionString;
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override async Task OpenAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (IsOpen) return;

            _Connection = new SqlConnection(ConnectionString);
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

            // SQL Server requires separate statements for table creation
            List<string> statements = new List<string>
            {
                // Schema version tracking
                @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='schema_version' AND xtype='U')
                CREATE TABLE schema_version (
                    version INT PRIMARY KEY,
                    applied_utc DATETIME2 NOT NULL,
                    description NVARCHAR(500)
                )",

                // User master
                @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='user_master' AND xtype='U')
                CREATE TABLE user_master (
                    guid NVARCHAR(36) PRIMARY KEY,
                    username NVARCHAR(255) NOT NULL UNIQUE,
                    email NVARCHAR(255),
                    first_name NVARCHAR(255),
                    last_name NVARCHAR(255),
                    active BIT NOT NULL DEFAULT 1,
                    is_admin BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL,
                    modified_utc DATETIME2,
                    last_login_utc DATETIME2
                )",

                // Credentials (bearer tokens)
                @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='credentials' AND xtype='U')
                CREATE TABLE credentials (
                    guid NVARCHAR(36) PRIMARY KEY,
                    user_guid NVARCHAR(36) NOT NULL,
                    name NVARCHAR(255),
                    description NVARCHAR(MAX),
                    bearer_token_hash NVARCHAR(255) NOT NULL,
                    active BIT NOT NULL DEFAULT 1,
                    is_read_only BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL,
                    expires_utc DATETIME2,
                    last_used_utc DATETIME2,
                    CONSTRAINT FK_credentials_user FOREIGN KEY (user_guid) REFERENCES user_master(guid) ON DELETE CASCADE
                )",

                // Add is_read_only column if not exists (for schema migration)
                @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'credentials' AND COLUMN_NAME = 'is_read_only')
                ALTER TABLE credentials ADD is_read_only BIT NOT NULL DEFAULT 0",

                // Origin servers
                @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='origin_servers' AND xtype='U')
                CREATE TABLE origin_servers (
                    identifier NVARCHAR(255) PRIMARY KEY,
                    name NVARCHAR(255),
                    hostname NVARCHAR(255) NOT NULL,
                    port INT NOT NULL,
                    ssl BIT NOT NULL DEFAULT 0,
                    health_check_interval_ms INT NOT NULL DEFAULT 5000,
                    health_check_method NVARCHAR(10) NOT NULL DEFAULT 'HEAD',
                    health_check_url NVARCHAR(1000) NOT NULL DEFAULT '/',
                    unhealthy_threshold INT NOT NULL DEFAULT 2,
                    healthy_threshold INT NOT NULL DEFAULT 1,
                    max_parallel_requests INT NOT NULL DEFAULT 10,
                    rate_limit_requests_threshold INT NOT NULL DEFAULT 30,
                    log_request BIT NOT NULL DEFAULT 0,
                    log_request_body BIT NOT NULL DEFAULT 0,
                    log_response BIT NOT NULL DEFAULT 0,
                    log_response_body BIT NOT NULL DEFAULT 0,
                    capture_request_body BIT NOT NULL DEFAULT 0,
                    capture_response_body BIT NOT NULL DEFAULT 0,
                    capture_request_headers BIT NOT NULL DEFAULT 1,
                    capture_response_headers BIT NOT NULL DEFAULT 1,
                    max_capture_request_body_size INT NOT NULL DEFAULT 65536,
                    max_capture_response_body_size INT NOT NULL DEFAULT 65536,
                    created_utc DATETIME2 NOT NULL,
                    modified_utc DATETIME2
                )",

                // API endpoints
                @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='api_endpoints' AND xtype='U')
                CREATE TABLE api_endpoints (
                    identifier NVARCHAR(255) PRIMARY KEY,
                    name NVARCHAR(255),
                    timeout_ms INT NOT NULL DEFAULT 60000,
                    load_balancing_mode NVARCHAR(50) NOT NULL DEFAULT 'RoundRobin',
                    block_http10 BIT NOT NULL DEFAULT 0,
                    max_request_body_size BIGINT NOT NULL DEFAULT 536870912,
                    log_request_full BIT NOT NULL DEFAULT 0,
                    log_request_body BIT NOT NULL DEFAULT 0,
                    log_response_body BIT NOT NULL DEFAULT 0,
                    include_auth_context_header BIT NOT NULL DEFAULT 1,
                    auth_context_header NVARCHAR(255) DEFAULT 'x-sb-auth-context',
                    use_global_blocked_headers BIT NOT NULL DEFAULT 1,
                    capture_request_body BIT NOT NULL DEFAULT 0,
                    capture_response_body BIT NOT NULL DEFAULT 0,
                    capture_request_headers BIT NOT NULL DEFAULT 1,
                    capture_response_headers BIT NOT NULL DEFAULT 1,
                    max_capture_request_body_size INT NOT NULL DEFAULT 65536,
                    max_capture_response_body_size INT NOT NULL DEFAULT 65536,
                    created_utc DATETIME2 NOT NULL,
                    modified_utc DATETIME2
                )",

                // Endpoint-origin mappings
                @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='endpoint_origin_mappings' AND xtype='U')
                CREATE TABLE endpoint_origin_mappings (
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    endpoint_identifier NVARCHAR(255) NOT NULL,
                    origin_identifier NVARCHAR(255) NOT NULL,
                    sort_order INT NOT NULL DEFAULT 0,
                    CONSTRAINT FK_eom_endpoint FOREIGN KEY (endpoint_identifier) REFERENCES api_endpoints(identifier) ON DELETE CASCADE,
                    CONSTRAINT FK_eom_origin FOREIGN KEY (origin_identifier) REFERENCES origin_servers(identifier) ON DELETE CASCADE,
                    CONSTRAINT UQ_endpoint_origin UNIQUE (endpoint_identifier, origin_identifier)
                )",

                // Endpoint routes
                @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='endpoint_routes' AND xtype='U')
                CREATE TABLE endpoint_routes (
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    endpoint_identifier NVARCHAR(255) NOT NULL,
                    http_method NVARCHAR(10) NOT NULL,
                    url_pattern NVARCHAR(1000) NOT NULL,
                    requires_authentication BIT NOT NULL DEFAULT 0,
                    sort_order INT NOT NULL DEFAULT 0,
                    CONSTRAINT FK_routes_endpoint FOREIGN KEY (endpoint_identifier) REFERENCES api_endpoints(identifier) ON DELETE CASCADE
                )",

                // URL rewrite rules
                @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='url_rewrites' AND xtype='U')
                CREATE TABLE url_rewrites (
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    endpoint_identifier NVARCHAR(255) NOT NULL,
                    http_method NVARCHAR(10) NOT NULL,
                    source_pattern NVARCHAR(1000) NOT NULL,
                    target_pattern NVARCHAR(1000) NOT NULL,
                    CONSTRAINT FK_rewrites_endpoint FOREIGN KEY (endpoint_identifier) REFERENCES api_endpoints(identifier) ON DELETE CASCADE
                )",

                // Blocked headers
                @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='blocked_headers' AND xtype='U')
                CREATE TABLE blocked_headers (
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    header_name NVARCHAR(255) NOT NULL UNIQUE
                )",

                // Request history
                @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='request_history' AND xtype='U')
                CREATE TABLE request_history (
                    request_id NVARCHAR(36) PRIMARY KEY,
                    timestamp_utc DATETIME2 NOT NULL,
                    http_method NVARCHAR(10) NOT NULL,
                    request_path NVARCHAR(2000) NOT NULL,
                    query_string NVARCHAR(MAX),
                    endpoint_identifier NVARCHAR(255),
                    origin_identifier NVARCHAR(255),
                    client_ip NVARCHAR(45),
                    request_body_size BIGINT NOT NULL DEFAULT 0,
                    request_body NVARCHAR(MAX),
                    request_headers NVARCHAR(MAX),
                    status_code INT NOT NULL,
                    response_body_size BIGINT NOT NULL DEFAULT 0,
                    response_body NVARCHAR(MAX),
                    response_headers NVARCHAR(MAX),
                    duration_ms BIGINT NOT NULL,
                    was_authenticated BIT NOT NULL DEFAULT 0,
                    error_message NVARCHAR(MAX),
                    success BIT NOT NULL DEFAULT 0
                )",

                // Indexes
                @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='idx_credentials_user_guid')
                CREATE INDEX idx_credentials_user_guid ON credentials(user_guid)",

                @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='idx_credentials_token_hash')
                CREATE INDEX idx_credentials_token_hash ON credentials(bearer_token_hash)",

                @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='idx_endpoint_origin_endpoint')
                CREATE INDEX idx_endpoint_origin_endpoint ON endpoint_origin_mappings(endpoint_identifier)",

                @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='idx_endpoint_routes_endpoint')
                CREATE INDEX idx_endpoint_routes_endpoint ON endpoint_routes(endpoint_identifier)",

                @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='idx_url_rewrites_endpoint')
                CREATE INDEX idx_url_rewrites_endpoint ON url_rewrites(endpoint_identifier)",

                @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='idx_history_timestamp')
                CREATE INDEX idx_history_timestamp ON request_history(timestamp_utc)",

                @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='idx_history_endpoint')
                CREATE INDEX idx_history_endpoint ON request_history(endpoint_identifier)",

                @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='idx_history_status')
                CREATE INDEX idx_history_status ON request_history(status_code)"
            };

            foreach (string statement in statements)
            {
                using (SqlCommand cmd = _Connection!.CreateCommand())
                {
                    cmd.CommandText = statement;
                    await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }

            // Insert initial schema version
            try
            {
                using (SqlCommand cmd = _Connection!.CreateCommand())
                {
                    cmd.CommandText = @"
                        IF NOT EXISTS (SELECT 1 FROM schema_version WHERE version = 1)
                        INSERT INTO schema_version (version, applied_utc, description) VALUES (1, GETUTCDATE(), 'Initial schema')";
                    await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
            catch
            {
                // Ignore if already exists
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

            // Handle IDENTITY columns for auto-increment
            bool hasAutoIncrement = record is EndpointOriginMapping or EndpointRoute or UrlRewrite or BlockedHeader;
            string outputClause = hasAutoIncrement ? " OUTPUT INSERTED.id" : "";

            string sql = $"INSERT INTO {tableName} ({String.Join(", ", columns)}){outputClause} VALUES ({String.Join(", ", parameters)})";

            using (SqlCommand cmd = _Connection!.CreateCommand())
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

            using (SqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (SqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);

                using (SqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (SqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);

                using (SqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (SqlCommand cmd = _Connection!.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@guid", guid.ToString());

                using (SqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (SqlCommand cmd = _Connection!.CreateCommand())
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

            using (SqlCommand cmd = _Connection!.CreateCommand())
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

            using (SqlCommand cmd = _Connection!.CreateCommand())
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

            using (SqlCommand cmd = _Connection!.CreateCommand())
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

            using (SqlCommand cmd = _Connection!.CreateCommand())
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

            using (SqlCommand cmd = _Connection!.CreateCommand())
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
                    values["block_http10"] = e.BlockHttp10;
                    values["max_request_body_size"] = e.MaxRequestBodySize;
                    values["log_request_full"] = e.LogRequestFull;
                    values["log_request_body"] = e.LogRequestBody;
                    values["log_response_body"] = e.LogResponseBody;
                    values["include_auth_context_header"] = e.IncludeAuthContextHeader;
                    values["auth_context_header"] = e.AuthContextHeader;
                    values["use_global_blocked_headers"] = e.UseGlobalBlockedHeaders;
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

        private static T MapReaderToObject<T>(SqlDataReader reader) where T : class
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
                    BlockHttp10 = reader.GetBoolean(reader.GetOrdinal("block_http10")),
                    MaxRequestBodySize = (int)reader.GetInt64(reader.GetOrdinal("max_request_body_size")),
                    LogRequestFull = reader.GetBoolean(reader.GetOrdinal("log_request_full")),
                    LogRequestBody = reader.GetBoolean(reader.GetOrdinal("log_request_body")),
                    LogResponseBody = reader.GetBoolean(reader.GetOrdinal("log_response_body")),
                    IncludeAuthContextHeader = reader.GetBoolean(reader.GetOrdinal("include_auth_context_header")),
                    UseGlobalBlockedHeaders = reader.GetBoolean(reader.GetOrdinal("use_global_blocked_headers")),
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
