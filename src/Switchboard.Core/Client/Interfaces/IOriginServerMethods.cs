#nullable enable

namespace Switchboard.Core.Client.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core.Models;

    /// <summary>
    /// Interface for origin server operations.
    /// </summary>
    public interface IOriginServerMethods
    {
        /// <summary>
        /// Create an origin server.
        /// </summary>
        /// <param name="config">Origin server configuration.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created origin server configuration.</returns>
        Task<OriginServerConfig> CreateAsync(OriginServerConfig config, CancellationToken token = default);

        /// <summary>
        /// Get an origin server by GUID.
        /// </summary>
        /// <param name="guid">Origin server GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Origin server configuration or null if not found.</returns>
        Task<OriginServerConfig?> GetByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Get an origin server by identifier.
        /// </summary>
        /// <param name="identifier">Origin server identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Origin server configuration or null if not found.</returns>
        Task<OriginServerConfig?> GetByIdentifierAsync(string identifier, CancellationToken token = default);

        /// <summary>
        /// Get all origin servers.
        /// </summary>
        /// <param name="searchTerm">Optional search term for name or identifier.</param>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of origin server configurations.</returns>
        Task<List<OriginServerConfig>> GetAllAsync(string? searchTerm = null, int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Update an origin server.
        /// </summary>
        /// <param name="config">Origin server configuration to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated origin server configuration.</returns>
        Task<OriginServerConfig> UpdateAsync(OriginServerConfig config, CancellationToken token = default);

        /// <summary>
        /// Delete an origin server by GUID.
        /// </summary>
        /// <param name="guid">Origin server GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Delete an origin server by identifier.
        /// </summary>
        /// <param name="identifier">Origin server identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByIdentifierAsync(string identifier, CancellationToken token = default);

        /// <summary>
        /// Check if an origin server exists by GUID.
        /// </summary>
        /// <param name="guid">Origin server GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists, false otherwise.</returns>
        Task<bool> ExistsByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Check if an origin server exists by identifier.
        /// </summary>
        /// <param name="identifier">Origin server identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists, false otherwise.</returns>
        Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken token = default);

        /// <summary>
        /// Get the total count of origin servers.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total count.</returns>
        Task<int> CountAsync(CancellationToken token = default);
    }
}
