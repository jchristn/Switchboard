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
    /// API endpoint operations implementation.
    /// </summary>
    internal class ApiEndpointMethods : IApiEndpointMethods
    {
        #region Private-Members

        private readonly IDatabaseDriver _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate API endpoint methods.
        /// </summary>
        /// <param name="database">Database driver.</param>
        internal ApiEndpointMethods(IDatabaseDriver database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<ApiEndpointConfig> CreateAsync(ApiEndpointConfig config, CancellationToken token = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (String.IsNullOrWhiteSpace(config.Identifier))
                throw new ArgumentException("Identifier cannot be null or empty.", nameof(config));

            if (config.GUID == Guid.Empty)
                config.GUID = Guid.NewGuid();

            config.CreatedUtc = DateTime.UtcNow;
            config.ModifiedUtc = DateTime.UtcNow;

            return await _Database.InsertAsync(config, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ApiEndpointConfig?> GetByGuidAsync(Guid guid, CancellationToken token = default)
        {
            return await _Database.SelectByGuidAsync<ApiEndpointConfig>(guid, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ApiEndpointConfig?> GetByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(identifier))
                throw new ArgumentNullException(nameof(identifier));

            List<ApiEndpointConfig> configs = await _Database.SelectAsync<ApiEndpointConfig>(
                c => c.Identifier == identifier,
                token).ConfigureAwait(false);

            return configs.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<List<ApiEndpointConfig>> GetAllAsync(string? searchTerm = null, int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<ApiEndpointConfig> configs = await _Database.SelectAllAsync<ApiEndpointConfig>(token).ConfigureAwait(false);

            IEnumerable<ApiEndpointConfig> result = configs;

            if (!String.IsNullOrWhiteSpace(searchTerm))
            {
                result = result.Where(c =>
                    (c.Identifier != null && c.Identifier.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (c.Name != null && c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
            }

            result = result.Skip(skip);

            if (take.HasValue)
                result = result.Take(take.Value);

            return result.ToList();
        }

        /// <inheritdoc />
        public async Task<ApiEndpointConfig> UpdateAsync(ApiEndpointConfig config, CancellationToken token = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            config.ModifiedUtc = DateTime.UtcNow;

            return await _Database.UpdateAsync(config, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteByGuidAsync(Guid guid, CancellationToken token = default)
        {
            ApiEndpointConfig? config = await GetByGuidAsync(guid, token).ConfigureAwait(false);
            if (config != null)
            {
                await _Database.DeleteAsync(config, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task DeleteByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            ApiEndpointConfig? config = await GetByIdentifierAsync(identifier, token).ConfigureAwait(false);
            if (config != null)
            {
                await _Database.DeleteAsync(config, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByGuidAsync(Guid guid, CancellationToken token = default)
        {
            return await _Database.ExistsAsync<ApiEndpointConfig>(c => c.GUID == guid, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(identifier))
                return false;

            return await _Database.ExistsAsync<ApiEndpointConfig>(c => c.Identifier == identifier, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(CancellationToken token = default)
        {
            return (int)await _Database.CountAsync<ApiEndpointConfig>(token).ConfigureAwait(false);
        }

        #endregion
    }
}
