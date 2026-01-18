#nullable enable

namespace Switchboard.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Base class for database drivers.
    /// Provides common functionality for all database implementations.
    /// </summary>
    public abstract class DatabaseDriverBase : IDatabaseDriver
    {
        #region Public-Members

        /// <summary>
        /// Gets whether the database connection is open.
        /// </summary>
        public abstract bool IsOpen { get; }

        /// <summary>
        /// Gets the database type.
        /// </summary>
        public abstract DatabaseTypeEnum DatabaseType { get; }

        #endregion

        #region Protected-Members

        /// <summary>
        /// Connection string.
        /// </summary>
        protected string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Disposed flag.
        /// </summary>
        protected bool Disposed { get; set; } = false;

        /// <summary>
        /// Lock object for thread-safe operations.
        /// </summary>
        protected readonly object Lock = new object();

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public abstract Task OpenAsync(CancellationToken token = default);

        /// <inheritdoc />
        public abstract Task CloseAsync(CancellationToken token = default);

        /// <inheritdoc />
        public abstract Task InitializeSchemaAsync(CancellationToken token = default);

        /// <inheritdoc />
        public abstract Task<T> InsertAsync<T>(T record, CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<List<T>> SelectAllAsync<T>(CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<List<T>> SelectAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<T?> SelectByIdAsync<T>(string id, CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<T?> SelectByIdAsync<T>(int id, CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<T?> SelectByGuidAsync<T>(Guid guid, CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<T> UpdateAsync<T>(T record, CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task DeleteAsync<T>(T record, CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<bool> ExistsAsync<T>(string id, CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<bool> ExistsAsync<T>(int id, CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<bool> ExistsAsync<T>(Guid guid, CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<bool> ExistsAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<long> CountAsync<T>(CancellationToken token = default) where T : class;

        /// <inheritdoc />
        public abstract Task<long> CountAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default) where T : class;

        /// <summary>
        /// Dispose of the database driver.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Async dispose of the database driver.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">Whether disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                }
                Disposed = true;
            }
        }

        /// <summary>
        /// Async dispose implementation.
        /// </summary>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            await CloseAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Throws if the driver has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if disposed.</exception>
        protected void ThrowIfDisposed()
        {
            if (Disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// Throws if the connection is not open.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected.</exception>
        protected void ThrowIfNotConnected()
        {
            if (!IsOpen)
                throw new InvalidOperationException("Database connection is not open.");
        }

        #endregion
    }
}
