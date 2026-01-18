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
    /// Origin server operations implementation.
    /// </summary>
    internal class OriginServerMethods : IOriginServerMethods
    {
        #region Private-Members

        private readonly IDatabaseDriver _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate origin server methods.
        /// </summary>
        /// <param name="database">Database driver.</param>
        internal OriginServerMethods(IDatabaseDriver database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<OriginServerConfig> CreateAsync(OriginServerConfig config, CancellationToken token = default)
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
        public async Task<OriginServerConfig?> GetByGuidAsync(Guid guid, CancellationToken token = default)
        {
            return await _Database.SelectByGuidAsync<OriginServerConfig>(guid, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<OriginServerConfig?> GetByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(identifier))
                throw new ArgumentNullException(nameof(identifier));

            List<OriginServerConfig> configs = await _Database.SelectAsync<OriginServerConfig>(
                c => c.Identifier == identifier,
                token).ConfigureAwait(false);

            return configs.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<List<OriginServerConfig>> GetAllAsync(string? searchTerm = null, int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<OriginServerConfig> configs = await _Database.SelectAllAsync<OriginServerConfig>(token).ConfigureAwait(false);

            IEnumerable<OriginServerConfig> result = configs;

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
        public async Task<OriginServerConfig> UpdateAsync(OriginServerConfig config, CancellationToken token = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            config.ModifiedUtc = DateTime.UtcNow;

            return await _Database.UpdateAsync(config, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteByGuidAsync(Guid guid, CancellationToken token = default)
        {
            OriginServerConfig? config = await GetByGuidAsync(guid, token).ConfigureAwait(false);
            if (config != null)
            {
                await _Database.DeleteAsync(config, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task DeleteByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            OriginServerConfig? config = await GetByIdentifierAsync(identifier, token).ConfigureAwait(false);
            if (config != null)
            {
                await _Database.DeleteAsync(config, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByGuidAsync(Guid guid, CancellationToken token = default)
        {
            return await _Database.ExistsAsync<OriginServerConfig>(c => c.GUID == guid, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(identifier))
                return false;

            return await _Database.ExistsAsync<OriginServerConfig>(c => c.Identifier == identifier, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(CancellationToken token = default)
        {
            return (int)await _Database.CountAsync<OriginServerConfig>(token).ConfigureAwait(false);
        }

        #endregion
    }
}
