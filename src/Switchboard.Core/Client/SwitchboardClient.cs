#nullable enable

namespace Switchboard.Core.Client
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core.Client.Implementations;
    using Switchboard.Core.Client.Interfaces;
    using Switchboard.Core.Database;
    using Switchboard.Core.Database.Sqlite;

    /// <summary>
    /// Main client for Switchboard database operations.
    /// Provides access to all entity CRUD operations through a unified interface.
    /// </summary>
    public class SwitchboardClient : IDisposable, IAsyncDisposable
    {
        #region Public-Members

        /// <summary>
        /// Origin server operations.
        /// </summary>
        public IOriginServerMethods OriginServers { get; }

        /// <summary>
        /// API endpoint operations.
        /// </summary>
        public IApiEndpointMethods ApiEndpoints { get; }

        /// <summary>
        /// Endpoint route operations.
        /// </summary>
        public IEndpointRouteMethods EndpointRoutes { get; }

        /// <summary>
        /// Endpoint-origin mapping operations.
        /// </summary>
        public IEndpointOriginMappingMethods EndpointOriginMappings { get; }

        /// <summary>
        /// URL rewrite operations.
        /// </summary>
        public IUrlRewriteMethods UrlRewrites { get; }

        /// <summary>
        /// Blocked header operations.
        /// </summary>
        public IBlockedHeaderMethods BlockedHeaders { get; }

        /// <summary>
        /// User operations.
        /// </summary>
        public IUserMethods Users { get; }

        /// <summary>
        /// Credential operations.
        /// </summary>
        public ICredentialMethods Credentials { get; }

        /// <summary>
        /// Request history operations.
        /// </summary>
        public IRequestHistoryMethods RequestHistory { get; }

        /// <summary>
        /// Gets the underlying database driver.
        /// </summary>
        public IDatabaseDriver Database => _Database;

        /// <summary>
        /// Gets whether the client is connected to the database.
        /// </summary>
        public bool IsConnected => _Database.IsOpen;

        #endregion

        #region Private-Members

        private readonly IDatabaseDriver _Database;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate client with SQLite database.
        /// </summary>
        /// <param name="databasePath">Path to SQLite database file.</param>
        public SwitchboardClient(string databasePath)
            : this(new SqliteDatabaseDriver(databasePath))
        {
        }

        /// <summary>
        /// Instantiate client with custom database driver.
        /// </summary>
        /// <param name="database">Database driver.</param>
        public SwitchboardClient(IDatabaseDriver database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));

            OriginServers = new OriginServerMethods(_Database);
            ApiEndpoints = new ApiEndpointMethods(_Database);
            EndpointRoutes = new EndpointRouteMethods(_Database);
            EndpointOriginMappings = new EndpointOriginMappingMethods(_Database);
            UrlRewrites = new UrlRewriteMethods(_Database);
            BlockedHeaders = new BlockedHeaderMethods(_Database);
            Users = new UserMethods(_Database);
            Credentials = new CredentialMethods(_Database);
            RequestHistory = new RequestHistoryMethods(_Database);
        }

        /// <summary>
        /// Create and initialize a new SwitchboardClient asynchronously.
        /// </summary>
        /// <param name="databasePath">Path to SQLite database file.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Initialized SwitchboardClient.</returns>
        public static async Task<SwitchboardClient> CreateAsync(string databasePath, CancellationToken token = default)
        {
            SqliteDatabaseDriver driver = new SqliteDatabaseDriver(databasePath);
            return await CreateAsync(driver, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create and initialize a new SwitchboardClient asynchronously with a custom database driver.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Initialized SwitchboardClient.</returns>
        public static async Task<SwitchboardClient> CreateAsync(IDatabaseDriver database, CancellationToken token = default)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            SwitchboardClient client = new SwitchboardClient(database);
            await client.ConnectAsync(token).ConfigureAwait(false);
            return client;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Connect to the database and initialize the schema.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ConnectAsync(CancellationToken token = default)
        {
            if (!_Database.IsOpen)
            {
                await _Database.OpenAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Disconnect from the database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DisconnectAsync(CancellationToken token = default)
        {
            if (_Database.IsOpen)
            {
                await _Database.CloseAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Dispose of the client.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Async dispose of the client.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">Whether disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    _Database?.Dispose();
                }
                _Disposed = true;
            }
        }

        /// <summary>
        /// Async dispose implementation.
        /// </summary>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_Database != null)
            {
                await _Database.DisposeAsync().ConfigureAwait(false);
            }
        }

        #endregion
    }
}
