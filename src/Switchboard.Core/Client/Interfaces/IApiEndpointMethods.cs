#nullable enable

namespace Switchboard.Core.Client.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core.Models;

    /// <summary>
    /// Interface for API endpoint operations.
    /// </summary>
    public interface IApiEndpointMethods
    {
        /// <summary>
        /// Create an API endpoint.
        /// </summary>
        /// <param name="config">API endpoint configuration.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created API endpoint configuration.</returns>
        Task<ApiEndpointConfig> CreateAsync(ApiEndpointConfig config, CancellationToken token = default);

        /// <summary>
        /// Get an API endpoint by GUID.
        /// </summary>
        /// <param name="guid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API endpoint configuration or null if not found.</returns>
        Task<ApiEndpointConfig?> GetByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Get an API endpoint by identifier.
        /// </summary>
        /// <param name="identifier">API endpoint identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API endpoint configuration or null if not found.</returns>
        Task<ApiEndpointConfig?> GetByIdentifierAsync(string identifier, CancellationToken token = default);

        /// <summary>
        /// Get all API endpoints.
        /// </summary>
        /// <param name="searchTerm">Optional search term for name or identifier.</param>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of API endpoint configurations.</returns>
        Task<List<ApiEndpointConfig>> GetAllAsync(string? searchTerm = null, int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Update an API endpoint.
        /// </summary>
        /// <param name="config">API endpoint configuration to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated API endpoint configuration.</returns>
        Task<ApiEndpointConfig> UpdateAsync(ApiEndpointConfig config, CancellationToken token = default);

        /// <summary>
        /// Delete an API endpoint by GUID.
        /// </summary>
        /// <param name="guid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Delete an API endpoint by identifier.
        /// </summary>
        /// <param name="identifier">API endpoint identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByIdentifierAsync(string identifier, CancellationToken token = default);

        /// <summary>
        /// Check if an API endpoint exists by GUID.
        /// </summary>
        /// <param name="guid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists, false otherwise.</returns>
        Task<bool> ExistsByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Check if an API endpoint exists by identifier.
        /// </summary>
        /// <param name="identifier">API endpoint identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists, false otherwise.</returns>
        Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken token = default);

        /// <summary>
        /// Get the total count of API endpoints.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total count.</returns>
        Task<int> CountAsync(CancellationToken token = default);
    }
}
