#nullable enable

namespace Switchboard.Core.Client.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core.Models;

    /// <summary>
    /// Interface for endpoint-origin mapping operations.
    /// </summary>
    public interface IEndpointOriginMappingMethods
    {
        /// <summary>
        /// Create an endpoint-origin mapping.
        /// </summary>
        /// <param name="mapping">Mapping to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created mapping.</returns>
        Task<EndpointOriginMapping> CreateAsync(EndpointOriginMapping mapping, CancellationToken token = default);

        /// <summary>
        /// Get a mapping by ID.
        /// </summary>
        /// <param name="id">Mapping ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Mapping or null if not found.</returns>
        Task<EndpointOriginMapping?> GetByIdAsync(int id, CancellationToken token = default);

        /// <summary>
        /// Get all mappings for an API endpoint.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of mappings.</returns>
        Task<List<EndpointOriginMapping>> GetByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default);

        /// <summary>
        /// Get all mappings for an origin server.
        /// </summary>
        /// <param name="originGuid">Origin server GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of mappings.</returns>
        Task<List<EndpointOriginMapping>> GetByOriginGuidAsync(Guid originGuid, CancellationToken token = default);

        /// <summary>
        /// Get all origin servers for an API endpoint.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of origin server configurations.</returns>
        Task<List<OriginServerConfig>> GetOriginsForEndpointAsync(Guid endpointGuid, CancellationToken token = default);

        /// <summary>
        /// Get all API endpoints for an origin server.
        /// </summary>
        /// <param name="originGuid">Origin server GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of API endpoint configurations.</returns>
        Task<List<ApiEndpointConfig>> GetEndpointsForOriginAsync(Guid originGuid, CancellationToken token = default);

        /// <summary>
        /// Get all mappings.
        /// </summary>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of mappings.</returns>
        Task<List<EndpointOriginMapping>> GetAllAsync(int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Delete a mapping by ID.
        /// </summary>
        /// <param name="id">Mapping ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByIdAsync(int id, CancellationToken token = default);

        /// <summary>
        /// Delete all mappings for an API endpoint.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default);

        /// <summary>
        /// Delete all mappings for an origin server.
        /// </summary>
        /// <param name="originGuid">Origin server GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByOriginGuidAsync(Guid originGuid, CancellationToken token = default);

        /// <summary>
        /// Check if a mapping exists.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="originGuid">Origin server GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists, false otherwise.</returns>
        Task<bool> ExistsAsync(Guid endpointGuid, Guid originGuid, CancellationToken token = default);

        /// <summary>
        /// Get the total count of mappings.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total count.</returns>
        Task<int> CountAsync(CancellationToken token = default);
    }
}
