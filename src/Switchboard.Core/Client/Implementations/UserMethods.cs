#nullable enable

namespace Switchboard.Core.Client.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Switchboard.Core.Client.Interfaces;
    using Switchboard.Core.Database;
    using Switchboard.Core.Models;

    /// <summary>
    /// User operations implementation.
    /// </summary>
    internal class UserMethods : IUserMethods
    {
        #region Private-Members

        private readonly IDatabaseDriver _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate user methods.
        /// </summary>
        /// <param name="database">Database driver.</param>
        internal UserMethods(IDatabaseDriver database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<UserMaster> CreateAsync(UserMaster user, CancellationToken token = default)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (String.IsNullOrWhiteSpace(user.Username))
                throw new ArgumentException("Username cannot be null or empty.", nameof(user));

            if (user.GUID == Guid.Empty)
                user.GUID = Guid.NewGuid();

            user.CreatedUtc = DateTime.UtcNow;
            user.ModifiedUtc = DateTime.UtcNow;

            return await _Database.InsertAsync(user, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<UserMaster?> GetByGuidAsync(Guid guid, CancellationToken token = default)
        {
            return await _Database.SelectByGuidAsync<UserMaster>(guid, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<UserMaster?> GetByUsernameAsync(string username, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(username))
                throw new ArgumentNullException(nameof(username));

            List<UserMaster> users = await _Database.SelectAsync<UserMaster>(
                u => u.Username != null && u.Username.Equals(username, StringComparison.OrdinalIgnoreCase),
                token).ConfigureAwait(false);

            return users.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<UserMaster?> GetByEmailAsync(string email, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(email))
                throw new ArgumentNullException(nameof(email));

            List<UserMaster> users = await _Database.SelectAsync<UserMaster>(
                u => u.Email != null && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase),
                token).ConfigureAwait(false);

            return users.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<List<UserMaster>> GetAllAsync(string? searchTerm = null, int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<UserMaster> users = await _Database.SelectAllAsync<UserMaster>(token).ConfigureAwait(false);

            IEnumerable<UserMaster> result = users;

            if (!String.IsNullOrWhiteSpace(searchTerm))
            {
                result = result.Where(u =>
                    (u.Username != null && u.Username.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (u.Email != null && u.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (u.FirstName != null && u.FirstName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (u.LastName != null && u.LastName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
            }

            result = result.Skip(skip);

            if (take.HasValue)
                result = result.Take(take.Value);

            return result.ToList();
        }

        /// <inheritdoc />
        public async Task<List<UserMaster>> GetActiveUsersAsync(CancellationToken token = default)
        {
            return await _Database.SelectAsync<UserMaster>(u => u.Active, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<UserMaster>> GetAdminUsersAsync(CancellationToken token = default)
        {
            return await _Database.SelectAsync<UserMaster>(u => u.IsAdmin, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<UserMaster> UpdateAsync(UserMaster user, CancellationToken token = default)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            user.ModifiedUtc = DateTime.UtcNow;

            return await _Database.UpdateAsync(user, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteByGuidAsync(Guid guid, CancellationToken token = default)
        {
            UserMaster? user = await GetByGuidAsync(guid, token).ConfigureAwait(false);
            if (user != null)
            {
                await _Database.DeleteAsync(user, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task DeleteByUsernameAsync(string username, CancellationToken token = default)
        {
            UserMaster? user = await GetByUsernameAsync(username, token).ConfigureAwait(false);
            if (user != null)
            {
                await _Database.DeleteAsync(user, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByGuidAsync(Guid guid, CancellationToken token = default)
        {
            return await _Database.ExistsAsync<UserMaster>(u => u.GUID == guid, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(username))
                return false;

            return await _Database.ExistsAsync<UserMaster>(
                u => u.Username != null && u.Username.Equals(username, StringComparison.OrdinalIgnoreCase),
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByEmailAsync(string email, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(email))
                return false;

            return await _Database.ExistsAsync<UserMaster>(
                u => u.Email != null && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase),
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(CancellationToken token = default)
        {
            return (int)await _Database.CountAsync<UserMaster>(token).ConfigureAwait(false);
        }

        #endregion
    }
}
