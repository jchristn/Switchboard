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
    /// Endpoint route operations implementation.
    /// </summary>
    internal class EndpointRouteMethods : IEndpointRouteMethods
    {
        #region Private-Members

        private readonly IDatabaseDriver _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate endpoint route methods.
        /// </summary>
        /// <param name="database">Database driver.</param>
        internal EndpointRouteMethods(IDatabaseDriver database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<EndpointRoute> CreateAsync(EndpointRoute route, CancellationToken token = default)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            if (route.EndpointGUID == Guid.Empty)
                throw new ArgumentException("EndpointGUID cannot be empty.", nameof(route));

            route.CreatedUtc = DateTime.UtcNow;

            return await _Database.InsertAsync(route, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EndpointRoute?> GetByIdAsync(int id, CancellationToken token = default)
        {
            return await _Database.SelectByIdAsync<EndpointRoute>(id, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<EndpointRoute>> GetByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default)
        {
            List<EndpointRoute> routes = await _Database.SelectAsync<EndpointRoute>(
                r => r.EndpointGUID == endpointGuid,
                token).ConfigureAwait(false);

            return routes.OrderBy(r => r.SortOrder).ToList();
        }

        /// <inheritdoc />
        public async Task<List<EndpointRoute>> GetAllAsync(int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<EndpointRoute> routes = await _Database.SelectAllAsync<EndpointRoute>(token).ConfigureAwait(false);

            IEnumerable<EndpointRoute> result = routes.Skip(skip);

            if (take.HasValue)
                result = result.Take(take.Value);

            return result.ToList();
        }

        /// <inheritdoc />
        public async Task<EndpointRoute> UpdateAsync(EndpointRoute route, CancellationToken token = default)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            return await _Database.UpdateAsync(route, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteByIdAsync(int id, CancellationToken token = default)
        {
            EndpointRoute? route = await GetByIdAsync(id, token).ConfigureAwait(false);
            if (route != null)
            {
                await _Database.DeleteAsync(route, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task DeleteByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default)
        {
            List<EndpointRoute> routes = await GetByEndpointGuidAsync(endpointGuid, token).ConfigureAwait(false);
            foreach (EndpointRoute route in routes)
            {
                await _Database.DeleteAsync(route, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(CancellationToken token = default)
        {
            return (int)await _Database.CountAsync<EndpointRoute>(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> CountByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default)
        {
            List<EndpointRoute> routes = await GetByEndpointGuidAsync(endpointGuid, token).ConfigureAwait(false);
            return routes.Count;
        }

        #endregion
    }
}
