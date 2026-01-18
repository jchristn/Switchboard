#nullable enable

namespace Switchboard.Core.Client.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core.Models;

    /// <summary>
    /// Interface for credential operations.
    /// </summary>
    public interface ICredentialMethods
    {
        /// <summary>
        /// Create a credential.
        /// </summary>
        /// <param name="credential">Credential to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created credential with generated bearer token.</returns>
        Task<Credential> CreateAsync(Credential credential, CancellationToken token = default);

        /// <summary>
        /// Get a credential by GUID.
        /// </summary>
        /// <param name="guid">Credential GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential or null if not found.</returns>
        Task<Credential?> GetByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Get all credentials for a user.
        /// </summary>
        /// <param name="userGuid">User GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of credentials.</returns>
        Task<List<Credential>> GetByUserGuidAsync(Guid userGuid, CancellationToken token = default);

        /// <summary>
        /// Get all credentials.
        /// </summary>
        /// <param name="searchTerm">Optional search term for name or description.</param>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of credentials.</returns>
        Task<List<Credential>> GetAllAsync(string? searchTerm = null, int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Get all active credentials.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of active credentials.</returns>
        Task<List<Credential>> GetActiveCredentialsAsync(CancellationToken token = default);

        /// <summary>
        /// Validate a bearer token.
        /// </summary>
        /// <param name="bearerToken">Bearer token to validate.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential if valid, null otherwise.</returns>
        Task<Credential?> ValidateBearerTokenAsync(string bearerToken, CancellationToken token = default);

        /// <summary>
        /// Get the user associated with a bearer token.
        /// </summary>
        /// <param name="bearerToken">Bearer token.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User if token is valid, null otherwise.</returns>
        Task<UserMaster?> GetUserByBearerTokenAsync(string bearerToken, CancellationToken token = default);

        /// <summary>
        /// Update a credential.
        /// </summary>
        /// <param name="credential">Credential to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated credential.</returns>
        Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default);

        /// <summary>
        /// Regenerate bearer token for a credential.
        /// </summary>
        /// <param name="guid">Credential GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated credential with new bearer token.</returns>
        Task<Credential> RegenerateBearerTokenAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Delete a credential by GUID.
        /// </summary>
        /// <param name="guid">Credential GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Delete all credentials for a user.
        /// </summary>
        /// <param name="userGuid">User GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByUserGuidAsync(Guid userGuid, CancellationToken token = default);

        /// <summary>
        /// Check if a credential exists by GUID.
        /// </summary>
        /// <param name="guid">Credential GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists, false otherwise.</returns>
        Task<bool> ExistsByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Get the total count of credentials.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total count.</returns>
        Task<int> CountAsync(CancellationToken token = default);

        /// <summary>
        /// Get the count of credentials for a user.
        /// </summary>
        /// <param name="userGuid">User GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential count.</returns>
        Task<int> CountByUserGuidAsync(Guid userGuid, CancellationToken token = default);
    }
}
