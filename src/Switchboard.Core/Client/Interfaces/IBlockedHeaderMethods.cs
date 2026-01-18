#nullable enable

namespace Switchboard.Core.Client.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core.Models;

    /// <summary>
    /// Interface for blocked header operations.
    /// </summary>
    public interface IBlockedHeaderMethods
    {
        /// <summary>
        /// Create a blocked header.
        /// </summary>
        /// <param name="header">Blocked header to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created blocked header.</returns>
        Task<BlockedHeader> CreateAsync(BlockedHeader header, CancellationToken token = default);

        /// <summary>
        /// Get a blocked header by ID.
        /// </summary>
        /// <param name="id">Blocked header ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Blocked header or null if not found.</returns>
        Task<BlockedHeader?> GetByIdAsync(int id, CancellationToken token = default);

        /// <summary>
        /// Get a blocked header by name.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Blocked header or null if not found.</returns>
        Task<BlockedHeader?> GetByNameAsync(string headerName, CancellationToken token = default);

        /// <summary>
        /// Get all blocked headers.
        /// </summary>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of blocked headers.</returns>
        Task<List<BlockedHeader>> GetAllAsync(int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Delete a blocked header by ID.
        /// </summary>
        /// <param name="id">Blocked header ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByIdAsync(int id, CancellationToken token = default);

        /// <summary>
        /// Delete a blocked header by name.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByNameAsync(string headerName, CancellationToken token = default);

        /// <summary>
        /// Check if a header is blocked.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if blocked, false otherwise.</returns>
        Task<bool> IsBlockedAsync(string headerName, CancellationToken token = default);

        /// <summary>
        /// Get the total count of blocked headers.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total count.</returns>
        Task<int> CountAsync(CancellationToken token = default);
    }
}
