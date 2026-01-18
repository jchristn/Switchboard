#nullable enable

namespace Switchboard.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Database driver interface.
    /// All database implementations must implement this interface.
    /// </summary>
    public interface IDatabaseDriver : IDisposable, IAsyncDisposable
    {
        #region Properties

        /// <summary>
        /// Gets whether the database connection is open.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Gets the database type.
        /// </summary>
        DatabaseTypeEnum DatabaseType { get; }

        #endregion

        #region Connection-Methods

        /// <summary>
        /// Opens the database connection.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task OpenAsync(CancellationToken token = default);

        /// <summary>
        /// Closes the database connection.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task CloseAsync(CancellationToken token = default);

        #endregion

        #region Schema-Methods

        /// <summary>
        /// Initializes the database schema.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task InitializeSchemaAsync(CancellationToken token = default);

        #endregion

        #region CRUD-Methods

        /// <summary>
        /// Insert a record.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="record">Record to insert.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Inserted record.</returns>
        Task<T> InsertAsync<T>(T record, CancellationToken token = default) where T : class;

        /// <summary>
        /// Select all records of a type.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of all records.</returns>
        Task<List<T>> SelectAllAsync<T>(CancellationToken token = default) where T : class;

        /// <summary>
        /// Select records matching a predicate.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="predicate">Filter predicate.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of matching records.</returns>
        Task<List<T>> SelectAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default) where T : class;

        /// <summary>
        /// Select a record by its identifier.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="id">Record identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record or null if not found.</returns>
        Task<T?> SelectByIdAsync<T>(string id, CancellationToken token = default) where T : class;

        /// <summary>
        /// Select a record by its integer identifier.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="id">Record identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record or null if not found.</returns>
        Task<T?> SelectByIdAsync<T>(int id, CancellationToken token = default) where T : class;

        /// <summary>
        /// Select a record by its GUID identifier.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="guid">Record GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record or null if not found.</returns>
        Task<T?> SelectByGuidAsync<T>(Guid guid, CancellationToken token = default) where T : class;

        /// <summary>
        /// Update a record.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="record">Record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated record.</returns>
        Task<T> UpdateAsync<T>(T record, CancellationToken token = default) where T : class;

        /// <summary>
        /// Delete a record.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="record">Record to delete.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync<T>(T record, CancellationToken token = default) where T : class;

        /// <summary>
        /// Delete records matching a predicate.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="predicate">Filter predicate.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of records deleted.</returns>
        Task<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default) where T : class;

        /// <summary>
        /// Check if a record exists by its string identifier.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="id">Record identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the record exists.</returns>
        Task<bool> ExistsAsync<T>(string id, CancellationToken token = default) where T : class;

        /// <summary>
        /// Check if a record exists by its integer identifier.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="id">Record identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the record exists.</returns>
        Task<bool> ExistsAsync<T>(int id, CancellationToken token = default) where T : class;

        /// <summary>
        /// Check if a record exists by its GUID.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="guid">Record GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the record exists.</returns>
        Task<bool> ExistsAsync<T>(Guid guid, CancellationToken token = default) where T : class;

        /// <summary>
        /// Check if a record exists matching a predicate.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="predicate">Filter predicate.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if any record matches.</returns>
        Task<bool> ExistsAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default) where T : class;

        /// <summary>
        /// Count all records of a type.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of records.</returns>
        Task<long> CountAsync<T>(CancellationToken token = default) where T : class;

        /// <summary>
        /// Count records matching a predicate.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="predicate">Filter predicate.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of matching records.</returns>
        Task<long> CountAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken token = default) where T : class;

        #endregion
    }
}
