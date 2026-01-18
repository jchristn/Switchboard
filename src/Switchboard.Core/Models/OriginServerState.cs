#nullable enable

namespace Switchboard.Core.Models
{
    using System;
    using System.Text.Json.Serialization;
    using System.Threading;

    /// <summary>
    /// Runtime state for an origin server.
    /// This class contains only runtime state data that is NOT stored in the database.
    /// Configuration is maintained separately in OriginServerConfig.
    /// </summary>
    public class OriginServerState : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Origin server identifier (matches OriginServerConfig.Identifier).
        /// </summary>
        public string Identifier
        {
            get => _Identifier;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Identifier));
                _Identifier = value;
            }
        }

        /// <summary>
        /// Boolean indicating if the backend is healthy.
        /// </summary>
        public bool Healthy { get; set; } = false;

        /// <summary>
        /// Number of consecutive successful health checks.
        /// </summary>
        public int HealthCheckSuccess { get; set; } = 0;

        /// <summary>
        /// Number of consecutive failed health checks.
        /// </summary>
        public int HealthCheckFailure { get; set; } = 0;

        /// <summary>
        /// Number of currently active requests.
        /// </summary>
        public int ActiveRequests { get; set; } = 0;

        /// <summary>
        /// Number of pending requests.
        /// </summary>
        public int PendingRequests { get; set; } = 0;

        /// <summary>
        /// Total requests processed.
        /// </summary>
        public long TotalRequests { get; set; } = 0;

        /// <summary>
        /// Total successful requests (2xx status codes).
        /// </summary>
        public long SuccessfulRequests { get; set; } = 0;

        /// <summary>
        /// Total failed requests (4xx or 5xx status codes).
        /// </summary>
        public long FailedRequests { get; set; } = 0;

        /// <summary>
        /// Lock object for thread-safe operations.
        /// </summary>
        [JsonIgnore]
        public object Lock { get; } = new object();

        /// <summary>
        /// Semaphore for limiting concurrent requests.
        /// </summary>
        [JsonIgnore]
        public SemaphoreSlim? Semaphore
        {
            get => _Semaphore;
            set => _Semaphore = value;
        }

        #endregion

        #region Private-Members

        private string _Identifier = string.Empty;
        private SemaphoreSlim? _Semaphore = null;
        private bool _Disposed = false;
        private int _ActiveRequestsInternal = 0;
        private int _PendingRequestsInternal = 0;
        private long _TotalRequestsInternal = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OriginServerState()
        {
        }

        /// <summary>
        /// Instantiate with identifier and max parallel requests.
        /// </summary>
        /// <param name="identifier">Origin server identifier.</param>
        /// <param name="maxParallelRequests">Maximum parallel requests.</param>
        public OriginServerState(string identifier, int maxParallelRequests = 10)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            _Semaphore = new SemaphoreSlim(maxParallelRequests, maxParallelRequests);
        }

        /// <summary>
        /// Create state from configuration.
        /// </summary>
        /// <param name="config">Origin server configuration.</param>
        /// <returns>New state object.</returns>
        public static OriginServerState FromConfig(OriginServerConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return new OriginServerState(config.Identifier, config.MaxParallelRequests);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Increment active request count atomically.
        /// </summary>
        /// <returns>New count.</returns>
        public int IncrementActiveRequests()
        {
            return Interlocked.Increment(ref _ActiveRequestsInternal);
        }

        /// <summary>
        /// Decrement active request count atomically.
        /// </summary>
        /// <returns>New count.</returns>
        public int DecrementActiveRequests()
        {
            return Interlocked.Decrement(ref _ActiveRequestsInternal);
        }

        /// <summary>
        /// Increment pending request count atomically.
        /// </summary>
        /// <returns>New count.</returns>
        public int IncrementPendingRequests()
        {
            return Interlocked.Increment(ref _PendingRequestsInternal);
        }

        /// <summary>
        /// Decrement pending request count atomically.
        /// </summary>
        /// <returns>New count.</returns>
        public int DecrementPendingRequests()
        {
            return Interlocked.Decrement(ref _PendingRequestsInternal);
        }

        /// <summary>
        /// Increment total request count atomically.
        /// </summary>
        /// <returns>New count.</returns>
        public long IncrementTotalRequests()
        {
            return Interlocked.Increment(ref _TotalRequestsInternal);
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">Whether disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    _Semaphore?.Dispose();
                    _Semaphore = null;
                }
                _Disposed = true;
            }
        }

        #endregion
    }
}
