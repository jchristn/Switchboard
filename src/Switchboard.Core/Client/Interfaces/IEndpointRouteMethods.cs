#nullable enable

namespace Switchboard.Core.Client.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core.Models;

    /// <summary>
    /// Interface for endpoint route operations.
    /// </summary>
    public interface IEndpointRouteMethods
    {
        /// <summary>
        /// Create an endpoint route.
        /// </summary>
        /// <param name="route">Endpoint route to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created endpoint route.</returns>
        Task<EndpointRoute> CreateAsync(EndpointRoute route, CancellationToken token = default);

        /// <summary>
        /// Get an endpoint route by ID.
        /// </summary>
        /// <param name="id">Endpoint route ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Endpoint route or null if not found.</returns>
        Task<EndpointRoute?> GetByIdAsync(int id, CancellationToken token = default);

        /// <summary>
        /// Get all routes for an API endpoint.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of endpoint routes.</returns>
        Task<List<EndpointRoute>> GetByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default);

        /// <summary>
        /// Get all endpoint routes.
        /// </summary>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of endpoint routes.</returns>
        Task<List<EndpointRoute>> GetAllAsync(int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Update an endpoint route.
        /// </summary>
        /// <param name="route">Endpoint route to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated endpoint route.</returns>
        Task<EndpointRoute> UpdateAsync(EndpointRoute route, CancellationToken token = default);

        /// <summary>
        /// Delete an endpoint route by ID.
        /// </summary>
        /// <param name="id">Endpoint route ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByIdAsync(int id, CancellationToken token = default);

        /// <summary>
        /// Delete all routes for an API endpoint.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default);

        /// <summary>
        /// Get the total count of endpoint routes.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total count.</returns>
        Task<int> CountAsync(CancellationToken token = default);

        /// <summary>
        /// Get the count of routes for an API endpoint.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Route count.</returns>
        Task<int> CountByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default);
    }
}
