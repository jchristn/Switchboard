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
    /// URL rewrite operations implementation.
    /// </summary>
    internal class UrlRewriteMethods : IUrlRewriteMethods
    {
        #region Private-Members

        private readonly IDatabaseDriver _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate URL rewrite methods.
        /// </summary>
        /// <param name="database">Database driver.</param>
        internal UrlRewriteMethods(IDatabaseDriver database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<UrlRewrite> CreateAsync(UrlRewrite rewrite, CancellationToken token = default)
        {
            if (rewrite == null)
                throw new ArgumentNullException(nameof(rewrite));

            if (rewrite.EndpointGUID == Guid.Empty)
                throw new ArgumentException("EndpointGUID cannot be empty.", nameof(rewrite));

            rewrite.CreatedUtc = DateTime.UtcNow;

            return await _Database.InsertAsync(rewrite, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<UrlRewrite?> GetByIdAsync(int id, CancellationToken token = default)
        {
            return await _Database.SelectByIdAsync<UrlRewrite>(id, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<UrlRewrite>> GetByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default)
        {
            List<UrlRewrite> rewrites = await _Database.SelectAsync<UrlRewrite>(
                r => r.EndpointGUID == endpointGuid,
                token).ConfigureAwait(false);

            return rewrites.OrderBy(r => r.SortOrder).ToList();
        }

        /// <inheritdoc />
        public async Task<List<UrlRewrite>> GetAllAsync(int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<UrlRewrite> rewrites = await _Database.SelectAllAsync<UrlRewrite>(token).ConfigureAwait(false);

            IEnumerable<UrlRewrite> result = rewrites.Skip(skip);

            if (take.HasValue)
                result = result.Take(take.Value);

            return result.ToList();
        }

        /// <inheritdoc />
        public async Task<UrlRewrite> UpdateAsync(UrlRewrite rewrite, CancellationToken token = default)
        {
            if (rewrite == null)
                throw new ArgumentNullException(nameof(rewrite));

            return await _Database.UpdateAsync(rewrite, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteByIdAsync(int id, CancellationToken token = default)
        {
            UrlRewrite? rewrite = await GetByIdAsync(id, token).ConfigureAwait(false);
            if (rewrite != null)
            {
                await _Database.DeleteAsync(rewrite, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task DeleteByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default)
        {
            List<UrlRewrite> rewrites = await GetByEndpointGuidAsync(endpointGuid, token).ConfigureAwait(false);
            foreach (UrlRewrite rewrite in rewrites)
            {
                await _Database.DeleteAsync(rewrite, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(CancellationToken token = default)
        {
            return (int)await _Database.CountAsync<UrlRewrite>(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> CountByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default)
        {
            List<UrlRewrite> rewrites = await GetByEndpointGuidAsync(endpointGuid, token).ConfigureAwait(false);
            return rewrites.Count;
        }

        #endregion
    }
}
