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
    /// Request history operations implementation.
    /// </summary>
    internal class RequestHistoryMethods : IRequestHistoryMethods
    {
        #region Private-Members

        private readonly IDatabaseDriver _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate request history methods.
        /// </summary>
        /// <param name="database">Database driver.</param>
        internal RequestHistoryMethods(IDatabaseDriver database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<RequestHistory> CreateAsync(RequestHistory history, CancellationToken token = default)
        {
            if (history == null)
                throw new ArgumentNullException(nameof(history));

            if (history.GUID == Guid.Empty)
                history.GUID = Guid.NewGuid();

            history.TimestampUtc = DateTime.UtcNow;

            return await _Database.InsertAsync(history, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<RequestHistory?> GetByIdAsync(long id, CancellationToken token = default)
        {
            return await _Database.SelectByIdAsync<RequestHistory>((int)id, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<RequestHistory?> GetByGuidAsync(Guid guid, CancellationToken token = default)
        {
            return await _Database.SelectByGuidAsync<RequestHistory>(guid, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<RequestHistory>> GetByEndpointGuidAsync(Guid endpointGuid, int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<RequestHistory> history = await _Database.SelectAsync<RequestHistory>(
                h => h.EndpointGUID == endpointGuid,
                token).ConfigureAwait(false);

            IEnumerable<RequestHistory> result = history.OrderByDescending(h => h.TimestampUtc).Skip(skip);

            if (take.HasValue)
                result = result.Take(take.Value);

            return result.ToList();
        }

        /// <inheritdoc />
        public async Task<List<RequestHistory>> GetByOriginGuidAsync(Guid originGuid, int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<RequestHistory> history = await _Database.SelectAsync<RequestHistory>(
                h => h.OriginGUID == originGuid,
                token).ConfigureAwait(false);

            IEnumerable<RequestHistory> result = history.OrderByDescending(h => h.TimestampUtc).Skip(skip);

            if (take.HasValue)
                result = result.Take(take.Value);

            return result.ToList();
        }

        /// <inheritdoc />
        public async Task<List<RequestHistory>> GetByTimeRangeAsync(DateTime startUtc, DateTime endUtc, int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<RequestHistory> history = await _Database.SelectAsync<RequestHistory>(
                h => h.TimestampUtc >= startUtc && h.TimestampUtc <= endUtc,
                token).ConfigureAwait(false);

            IEnumerable<RequestHistory> result = history.OrderByDescending(h => h.TimestampUtc).Skip(skip);

            if (take.HasValue)
                result = result.Take(take.Value);

            return result.ToList();
        }

        /// <inheritdoc />
        public async Task<List<RequestHistory>> GetAllAsync(int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<RequestHistory> history = await _Database.SelectAllAsync<RequestHistory>(token).ConfigureAwait(false);

            IEnumerable<RequestHistory> result = history.OrderByDescending(h => h.TimestampUtc).Skip(skip);

            if (take.HasValue)
                result = result.Take(take.Value);

            return result.ToList();
        }

        /// <inheritdoc />
        public async Task<List<RequestHistory>> GetRecentAsync(int count = 100, CancellationToken token = default)
        {
            List<RequestHistory> history = await _Database.SelectAllAsync<RequestHistory>(token).ConfigureAwait(false);
            return history.OrderByDescending(h => h.TimestampUtc).Take(count).ToList();
        }

        /// <inheritdoc />
        public async Task<List<RequestHistory>> GetFailedRequestsAsync(int skip = 0, int? take = null, CancellationToken token = default)
        {
            List<RequestHistory> history = await _Database.SelectAsync<RequestHistory>(
                h => !h.Success,
                token).ConfigureAwait(false);

            IEnumerable<RequestHistory> result = history.OrderByDescending(h => h.TimestampUtc).Skip(skip);

            if (take.HasValue)
                result = result.Take(take.Value);

            return result.ToList();
        }

        /// <inheritdoc />
        public async Task DeleteByIdAsync(long id, CancellationToken token = default)
        {
            RequestHistory? history = await GetByIdAsync(id, token).ConfigureAwait(false);
            if (history != null)
            {
                await _Database.DeleteAsync(history, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task DeleteByGuidAsync(Guid guid, CancellationToken token = default)
        {
            RequestHistory? history = await GetByGuidAsync(guid, token).ConfigureAwait(false);
            if (history != null)
            {
                await _Database.DeleteAsync(history, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteOlderThanAsync(DateTime olderThanUtc, CancellationToken token = default)
        {
            List<RequestHistory> history = await _Database.SelectAsync<RequestHistory>(
                h => h.TimestampUtc < olderThanUtc,
                token).ConfigureAwait(false);

            int count = history.Count;

            foreach (RequestHistory h in history)
            {
                await _Database.DeleteAsync(h, token).ConfigureAwait(false);
            }

            return count;
        }

        /// <inheritdoc />
        public async Task<int> DeleteByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default)
        {
            List<RequestHistory> history = await _Database.SelectAsync<RequestHistory>(
                h => h.EndpointGUID == endpointGuid,
                token).ConfigureAwait(false);

            int count = history.Count;

            foreach (RequestHistory h in history)
            {
                await _Database.DeleteAsync(h, token).ConfigureAwait(false);
            }

            return count;
        }

        /// <inheritdoc />
        public async Task<int> DeleteByOriginGuidAsync(Guid originGuid, CancellationToken token = default)
        {
            List<RequestHistory> history = await _Database.SelectAsync<RequestHistory>(
                h => h.OriginGUID == originGuid,
                token).ConfigureAwait(false);

            int count = history.Count;

            foreach (RequestHistory h in history)
            {
                await _Database.DeleteAsync(h, token).ConfigureAwait(false);
            }

            return count;
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(CancellationToken token = default)
        {
            return await _Database.CountAsync<RequestHistory>(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<long> CountByEndpointGuidAsync(Guid endpointGuid, CancellationToken token = default)
        {
            List<RequestHistory> history = await _Database.SelectAsync<RequestHistory>(
                h => h.EndpointGUID == endpointGuid,
                token).ConfigureAwait(false);
            return history.Count;
        }

        /// <inheritdoc />
        public async Task<long> CountByOriginGuidAsync(Guid originGuid, CancellationToken token = default)
        {
            List<RequestHistory> history = await _Database.SelectAsync<RequestHistory>(
                h => h.OriginGUID == originGuid,
                token).ConfigureAwait(false);
            return history.Count;
        }

        /// <inheritdoc />
        public async Task<long> CountFailedAsync(CancellationToken token = default)
        {
            List<RequestHistory> history = await _Database.SelectAsync<RequestHistory>(
                h => !h.Success,
                token).ConfigureAwait(false);
            return history.Count;
        }

        #endregion
    }
}
