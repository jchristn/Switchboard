#nullable enable

namespace Switchboard.Core.Client.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core.Models;

    /// <summary>
    /// Interface for URL rewrite operations.
    /// </summary>
    public interface IUrlRewriteMethods
    {
        /// <summary>
        /// Create a URL rewrite rule.
        /// </summary>
        /// <param name="rewrite">URL rewrite rule to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created URL rewrite rule.</returns>
        Task<UrlRewrite> CreateAsync(UrlRewrite rewrite, CancellationToken token = default);

        /// <summary>
        /// Get a URL rewrite rule by ID.
        /// </summary>
        /// <param name="id">URL rewrite ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>URL rewrite rule or null if not found.</returns>
        Task<UrlRewrite?> GetByIdAsync(int id, CancellationToken token = default);

        /// <summary>
        /// Get all URL rewrite rules for an API endpoint.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of URL rewrite rules.</returns>
        Task<List<UrlRewrite>> GetByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default);

        /// <summary>
        /// Get all URL rewrite rules.
        /// </summary>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of URL rewrite rules.</returns>
        Task<List<UrlRewrite>> GetAllAsync(int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Update a URL rewrite rule.
        /// </summary>
        /// <param name="rewrite">URL rewrite rule to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated URL rewrite rule.</returns>
        Task<UrlRewrite> UpdateAsync(UrlRewrite rewrite, CancellationToken token = default);

        /// <summary>
        /// Delete a URL rewrite rule by ID.
        /// </summary>
        /// <param name="id">URL rewrite ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByIdAsync(int id, CancellationToken token = default);

        /// <summary>
        /// Delete all URL rewrite rules for an API endpoint.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default);

        /// <summary>
        /// Get the total count of URL rewrite rules.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total count.</returns>
        Task<int> CountAsync(CancellationToken token = default);

        /// <summary>
        /// Get the count of URL rewrite rules for an API endpoint.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rewrite count.</returns>
        Task<int> CountByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default);
    }
}
