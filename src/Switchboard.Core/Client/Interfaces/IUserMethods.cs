#nullable enable

namespace Switchboard.Core.Client.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core.Models;

    /// <summary>
    /// Interface for user operations.
    /// </summary>
    public interface IUserMethods
    {
        /// <summary>
        /// Create a user.
        /// </summary>
        /// <param name="user">User to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created user.</returns>
        Task<UserMaster> CreateAsync(UserMaster user, CancellationToken token = default);

        /// <summary>
        /// Get a user by GUID.
        /// </summary>
        /// <param name="guid">User GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User or null if not found.</returns>
        Task<UserMaster?> GetByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Get a user by username.
        /// </summary>
        /// <param name="username">Username.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User or null if not found.</returns>
        Task<UserMaster?> GetByUsernameAsync(string username, CancellationToken token = default);

        /// <summary>
        /// Get a user by email.
        /// </summary>
        /// <param name="email">Email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User or null if not found.</returns>
        Task<UserMaster?> GetByEmailAsync(string email, CancellationToken token = default);

        /// <summary>
        /// Get all users.
        /// </summary>
        /// <param name="searchTerm">Optional search term for username, email, or name.</param>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of users.</returns>
        Task<List<UserMaster>> GetAllAsync(string? searchTerm = null, int skip = 0, int? take = null, CancellationToken token = default);

        /// <summary>
        /// Get all active users.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of active users.</returns>
        Task<List<UserMaster>> GetActiveUsersAsync(CancellationToken token = default);

        /// <summary>
        /// Get all admin users.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of admin users.</returns>
        Task<List<UserMaster>> GetAdminUsersAsync(CancellationToken token = default);

        /// <summary>
        /// Update a user.
        /// </summary>
        /// <param name="user">User to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated user.</returns>
        Task<UserMaster> UpdateAsync(UserMaster user, CancellationToken token = default);

        /// <summary>
        /// Delete a user by GUID.
        /// </summary>
        /// <param name="guid">User GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Delete a user by username.
        /// </summary>
        /// <param name="username">Username.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByUsernameAsync(string username, CancellationToken token = default);

        /// <summary>
        /// Check if a user exists by GUID.
        /// </summary>
        /// <param name="guid">User GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists, false otherwise.</returns>
        Task<bool> ExistsByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Check if a user exists by username.
        /// </summary>
        /// <param name="username">Username.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists, false otherwise.</returns>
        Task<bool> ExistsByUsernameAsync(string username, CancellationToken token = default);

        /// <summary>
        /// Check if a user exists by email.
        /// </summary>
        /// <param name="email">Email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists, false otherwise.</returns>
        Task<bool> ExistsByEmailAsync(string email, CancellationToken token = default);

        /// <summary>
        /// Get the total count of users.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total count.</returns>
        Task<int> CountAsync(CancellationToken token = default);
    }
}
