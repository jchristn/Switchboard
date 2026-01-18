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
    /// Blocked header operations implementation.
    /// </summary>
    internal class BlockedHeaderMethods : IBlockedHeaderMethods
    {
        #region Private-Members

        private readonly IDatabaseDriver _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate blocked header methods.
        /// </summary>
        /// <param name="database">Database driver.</param>
        internal BlockedHeaderMethods(IDatabaseDriver database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<BlockedHeader> CreateAsync(BlockedHeader header, CancellationToken token = default)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            if (String.IsNullOrWhiteSpace(header.HeaderName))
                throw new ArgumentException("HeaderName cannot be null or empty.", nameof(header));

            header.CreatedUtc = DateTime.UtcNow;

            return await _Database.InsertAsync(header, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<BlockedHeader?> GetByIdAsync(int id, CancellationToken token = default)
        {
            return await _Database.SelectByIdAsync<BlockedHeader>(id, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<BlockedHeader?> GetByNameAsync(string headerName, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(headerName))
                throw new ArgumentNullException(nameof(headerName));

            List<BlockedHeader> headers = await _Database.SelectAsync<BlockedHeader>(
                h => h.HeaderName != null && h.HeaderName.Equals(headerName, StringComparison.OrdinalIgnoreCase),
                token).ConfigureAwait(false);

            return headers.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<List<BlockedHeader>> GetAllAsync(int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<BlockedHeader> headers = await _Database.SelectAllAsync<BlockedHeader>(token).ConfigureAwait(false);

            IEnumerable<BlockedHeader> result = headers.Skip(skip);

            if (take.HasValue)
                result = result.Take(take.Value);

            return result.ToList();
        }

        /// <inheritdoc />
        public async Task DeleteByIdAsync(int id, CancellationToken token = default)
        {
            BlockedHeader? header = await GetByIdAsync(id, token).ConfigureAwait(false);
            if (header != null)
            {
                await _Database.DeleteAsync(header, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task DeleteByNameAsync(string headerName, CancellationToken token = default)
        {
            BlockedHeader? header = await GetByNameAsync(headerName, token).ConfigureAwait(false);
            if (header != null)
            {
                await _Database.DeleteAsync(header, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsBlockedAsync(string headerName, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(headerName))
                return false;

            return await _Database.ExistsAsync<BlockedHeader>(
                h => h.HeaderName != null && h.HeaderName.Equals(headerName, StringComparison.OrdinalIgnoreCase),
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(CancellationToken token = default)
        {
            return (int)await _Database.CountAsync<BlockedHeader>(token).ConfigureAwait(false);
        }

        #endregion
    }
}
