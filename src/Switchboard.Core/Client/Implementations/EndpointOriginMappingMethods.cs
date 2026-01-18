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
    /// Endpoint-origin mapping operations implementation.
    /// </summary>
    internal class EndpointOriginMappingMethods : IEndpointOriginMappingMethods
    {
        #region Private-Members

        private readonly IDatabaseDriver _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate endpoint-origin mapping methods.
        /// </summary>
        /// <param name="database">Database driver.</param>
        internal EndpointOriginMappingMethods(IDatabaseDriver database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<EndpointOriginMapping> CreateAsync(EndpointOriginMapping mapping, CancellationToken token = default)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            if (mapping.EndpointGUID == Guid.Empty)
                throw new ArgumentException("EndpointGUID cannot be empty.", nameof(mapping));

            if (mapping.OriginGUID == Guid.Empty)
                throw new ArgumentException("OriginGUID cannot be empty.", nameof(mapping));

            mapping.CreatedUtc = DateTime.UtcNow;

            return await _Database.InsertAsync(mapping, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EndpointOriginMapping?> GetByIdAsync(int id, CancellationToken token = default)
        {
            return await _Database.SelectByIdAsync<EndpointOriginMapping>(id, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<EndpointOriginMapping>> GetByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default)
        {
            return await _Database.SelectAsync<EndpointOriginMapping>(
                m => m.EndpointGUID == endpointGuid,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<EndpointOriginMapping>> GetByOriginGuidAsync(Guid originGuid, CancellationToken token = default)
        {
            return await _Database.SelectAsync<EndpointOriginMapping>(
                m => m.OriginGUID == originGuid,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<OriginServerConfig>> GetOriginsForEndpointAsync(Guid endpointGuid, CancellationToken token = default)
        {
            List<EndpointOriginMapping> mappings = await GetByEndpointGuidAsync(endpointGuid, token).ConfigureAwait(false);
            List<OriginServerConfig> origins = new List<OriginServerConfig>();

            foreach (EndpointOriginMapping mapping in mappings)
            {
                OriginServerConfig? origin = await _Database.SelectByGuidAsync<OriginServerConfig>(mapping.OriginGUID, token).ConfigureAwait(false);
                if (origin != null)
                {
                    origins.Add(origin);
                }
            }

            return origins;
        }

        /// <inheritdoc />
        public async Task<List<ApiEndpointConfig>> GetEndpointsForOriginAsync(Guid originGuid, CancellationToken token = default)
        {
            List<EndpointOriginMapping> mappings = await GetByOriginGuidAsync(originGuid, token).ConfigureAwait(false);
            List<ApiEndpointConfig> endpoints = new List<ApiEndpointConfig>();

            foreach (EndpointOriginMapping mapping in mappings)
            {
                ApiEndpointConfig? endpoint = await _Database.SelectByGuidAsync<ApiEndpointConfig>(mapping.EndpointGUID, token).ConfigureAwait(false);
                if (endpoint != null)
                {
                    endpoints.Add(endpoint);
                }
            }

            return endpoints;
        }

        /// <inheritdoc />
        public async Task<List<EndpointOriginMapping>> GetAllAsync(int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<EndpointOriginMapping> mappings = await _Database.SelectAllAsync<EndpointOriginMapping>(token).ConfigureAwait(false);

            IEnumerable<EndpointOriginMapping> result = mappings.Skip(skip);

            if (take.HasValue)
                result = result.Take(take.Value);

            return result.ToList();
        }

        /// <inheritdoc />
        public async Task DeleteByIdAsync(int id, CancellationToken token = default)
        {
            EndpointOriginMapping? mapping = await GetByIdAsync(id, token).ConfigureAwait(false);
            if (mapping != null)
            {
                await _Database.DeleteAsync(mapping, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task DeleteByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default)
        {
            List<EndpointOriginMapping> mappings = await GetByEndpointGuidAsync(endpointGuid, token).ConfigureAwait(false);
            foreach (EndpointOriginMapping mapping in mappings)
            {
                await _Database.DeleteAsync(mapping, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task DeleteByOriginGuidAsync(Guid originGuid, CancellationToken token = default)
        {
            List<EndpointOriginMapping> mappings = await GetByOriginGuidAsync(originGuid, token).ConfigureAwait(false);
            foreach (EndpointOriginMapping mapping in mappings)
            {
                await _Database.DeleteAsync(mapping, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(Guid endpointGuid, Guid originGuid, CancellationToken token = default)
        {
            return await _Database.ExistsAsync<EndpointOriginMapping>(
                m => m.EndpointGUID == endpointGuid && m.OriginGUID == originGuid,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(CancellationToken token = default)
        {
            return (int)await _Database.CountAsync<EndpointOriginMapping>(token).ConfigureAwait(false);
        }

        #endregion
    }
}
