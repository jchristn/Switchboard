#nullable enable

namespace Switchboard.Core.Client.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core.Models;

    /// <summary>
    /// Interface for request history operations.
    /// </summary>
    public interface IRequestHistoryMethods
    {
        /// <summary>
        /// Create a request history record.
        /// </summary>
        /// <param name="history">Request history to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created request history.</returns>
        Task<RequestHistory> CreateAsync(RequestHistory history, CancellationToken token = default);

        /// <summary>
        /// Get a request history record by ID.
        /// </summary>
        /// <param name="id">Request history ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request history or null if not found.</returns>
        Task<RequestHistory?> GetByIdAsync(long id, CancellationToken token = default);

        /// <summary>
        /// Get a request history record by GUID.
        /// </summary>
        /// <param name="guid">Request history GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request history or null if not found.</returns>
        Task<RequestHistory?> GetByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Get all request history for an API endpoint.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of request history records.</returns>
        Task<List<RequestHistory>> GetByEndpointGuidAsync(Guid endpointGuid, int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Get all request history for an origin server.
        /// </summary>
        /// <param name="originGuid">Origin server GUID.</param>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of request history records.</returns>
        Task<List<RequestHistory>> GetByOriginGuidAsync(Guid originGuid, int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Get request history within a time range.
        /// </summary>
        /// <param name="startUtc">Start of time range (UTC).</param>
        /// <param name="endUtc">End of time range (UTC).</param>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of request history records.</returns>
        Task<List<RequestHistory>> GetByTimeRangeAsync(DateTime startUtc, DateTime endUtc, int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Get all request history.
        /// </summary>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of request history records.</returns>
        Task<List<RequestHistory>> GetAllAsync(int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Get recent request history.
        /// </summary>
        /// <param name="count">Number of records to retrieve.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of recent request history records.</returns>
        Task<List<RequestHistory>> GetRecentAsync(int count = 100, CancellationToken token = default);

        /// <summary>
        /// Get failed requests.
        /// </summary>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of failed request history records.</returns>
        Task<List<RequestHistory>> GetFailedRequestsAsync(int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Delete request history by ID.
        /// </summary>
        /// <param name="id">Request history ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByIdAsync(long id, CancellationToken token = default);

        /// <summary>
        /// Delete request history by GUID.
        /// </summary>
        /// <param name="guid">Request history GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Delete request history older than a specified date.
        /// </summary>
        /// <param name="olderThanUtc">Delete records older than this date (UTC).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of records deleted.</returns>
        Task<int> DeleteOlderThanAsync(DateTime olderThanUtc, CancellationToken token = default);

        /// <summary>
        /// Delete all request history for an API endpoint.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of records deleted.</returns>
        Task<int> DeleteByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default);

        /// <summary>
        /// Delete all request history for an origin server.
        /// </summary>
        /// <param name="originGuid">Origin server GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of records deleted.</returns>
        Task<int> DeleteByOriginGuidAsync(Guid originGuid, CancellationToken token = default);

        /// <summary>
        /// Get the total count of request history records.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total count.</returns>
        Task<long> CountAsync(CancellationToken token = default);

        /// <summary>
        /// Get the count of request history for an API endpoint.
        /// </summary>
        /// <param name="endpointGuid">API endpoint GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request count.</returns>
        Task<long> CountByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default);

        /// <summary>
        /// Get the count of request history for an origin server.
        /// </summary>
        /// <param name="originGuid">Origin server GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request count.</returns>
        Task<long> CountByOriginGuidAsync(Guid originGuid, CancellationToken token = default);

        /// <summary>
        /// Get the count of failed requests.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Failed request count.</returns>
        Task<long> CountFailedAsync(CancellationToken token = default);
    }
}
