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
    /// Credential operations implementation.
    /// </summary>
    internal class CredentialMethods : ICredentialMethods
    {
        #region Private-Members

        private readonly IDatabaseDriver _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate credential methods.
        /// </summary>
        /// <param name="database">Database driver.</param>
        internal CredentialMethods(IDatabaseDriver database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Credential> CreateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null)
                throw new ArgumentNullException(nameof(credential));

            if (credential.UserGUID == Guid.Empty)
                throw new ArgumentException("UserGUID cannot be empty.", nameof(credential));

            if (credential.GUID == Guid.Empty)
                credential.GUID = Guid.NewGuid();

            // Generate bearer token if not provided
            if (String.IsNullOrWhiteSpace(credential.BearerToken))
            {
                credential.BearerToken = Credential.GenerateBearerToken();
            }

            // Compute hash
            credential.BearerTokenHash = Credential.ComputeTokenHash(credential.BearerToken);

            credential.CreatedUtc = DateTime.UtcNow;
            credential.ModifiedUtc = DateTime.UtcNow;

            return await _Database.InsertAsync(credential, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Credential?> GetByGuidAsync(Guid guid, CancellationToken token = default)
        {
            return await _Database.SelectByGuidAsync<Credential>(guid, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Credential>> GetByUserGuidAsync(Guid userGuid, CancellationToken token = default)
        {
            return await _Database.SelectAsync<Credential>(
                c => c.UserGUID == userGuid,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Credential>> GetAllAsync(string? searchTerm = null, int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<Credential> credentials = await _Database.SelectAllAsync<Credential>(token).ConfigureAwait(false);

            IEnumerable<Credential> result = credentials;

            if (!String.IsNullOrWhiteSpace(searchTerm))
            {
                result = result.Where(c =>
                    (c.Name != null && c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (c.Description != null && c.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
            }

            result = result.Skip(skip);

            if (take.HasValue)
                result = result.Take(take.Value);

            return result.ToList();
        }

        /// <inheritdoc />
        public async Task<List<Credential>> GetActiveCredentialsAsync(CancellationToken token = default)
        {
            List<Credential> credentials = await _Database.SelectAsync<Credential>(
                c => c.Active,
                token).ConfigureAwait(false);

            // Filter out expired credentials
            DateTime now = DateTime.UtcNow;
            return credentials.Where(c => !c.ExpiresUtc.HasValue || c.ExpiresUtc.Value > now).ToList();
        }

        /// <inheritdoc />
        public async Task<Credential?> ValidateBearerTokenAsync(string bearerToken, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(bearerToken))
                return null;

            string tokenHash = Credential.ComputeTokenHash(bearerToken);

            List<Credential> credentials = await _Database.SelectAsync<Credential>(
                c => c.BearerTokenHash == tokenHash && c.Active,
                token).ConfigureAwait(false);

            Credential? credential = credentials.FirstOrDefault();

            if (credential == null)
                return null;

            // Check expiration
            if (credential.ExpiresUtc.HasValue && credential.ExpiresUtc.Value <= DateTime.UtcNow)
                return null;

            // Update last used timestamp
            credential.LastUsedUtc = DateTime.UtcNow;
            await _Database.UpdateAsync(credential, token).ConfigureAwait(false);

            return credential;
        }

        /// <inheritdoc />
        public async Task<UserMaster?> GetUserByBearerTokenAsync(string bearerToken, CancellationToken token = default)
        {
            Credential? credential = await ValidateBearerTokenAsync(bearerToken, token).ConfigureAwait(false);
            if (credential == null)
                return null;

            return await _Database.SelectByGuidAsync<UserMaster>(credential.UserGUID, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null)
                throw new ArgumentNullException(nameof(credential));

            credential.ModifiedUtc = DateTime.UtcNow;

            return await _Database.UpdateAsync(credential, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Credential> RegenerateBearerTokenAsync(Guid guid, CancellationToken token = default)
        {
            Credential? credential = await GetByGuidAsync(guid, token).ConfigureAwait(false);
            if (credential == null)
                throw new KeyNotFoundException($"Credential with GUID {guid} not found.");

            credential.BearerToken = Credential.GenerateBearerToken();
            credential.BearerTokenHash = Credential.ComputeTokenHash(credential.BearerToken);
            credential.ModifiedUtc = DateTime.UtcNow;

            return await _Database.UpdateAsync(credential, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteByGuidAsync(Guid guid, CancellationToken token = default)
        {
            Credential? credential = await GetByGuidAsync(guid, token).ConfigureAwait(false);
            if (credential != null)
            {
                await _Database.DeleteAsync(credential, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task DeleteByUserGuidAsync(Guid userGuid, CancellationToken token = default)
        {
            List<Credential> credentials = await GetByUserGuidAsync(userGuid, token).ConfigureAwait(false);
            foreach (Credential credential in credentials)
            {
                await _Database.DeleteAsync(credential, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByGuidAsync(Guid guid, CancellationToken token = default)
        {
            return await _Database.ExistsAsync<Credential>(c => c.GUID == guid, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(CancellationToken token = default)
        {
            return (int)await _Database.CountAsync<Credential>(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> CountByUserGuidAsync(Guid userGuid, CancellationToken token = default)
        {
            List<Credential> credentials = await GetByUserGuidAsync(userGuid, token).ConfigureAwait(false);
            return credentials.Count;
        }

        #endregion
    }
}
