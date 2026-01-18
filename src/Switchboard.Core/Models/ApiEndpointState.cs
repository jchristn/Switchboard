#nullable enable

namespace Switchboard.Core.Models
{
    using System;
    using System.Text.Json.Serialization;
    using System.Threading;

    /// <summary>
    /// Runtime state for an API endpoint.
    /// This class contains only runtime state data that is NOT stored in the database.
    /// Configuration is maintained separately in ApiEndpointConfig.
    /// </summary>
    public class ApiEndpointState
    {
        #region Public-Members

        /// <summary>
        /// Endpoint identifier (matches ApiEndpointConfig.Identifier).
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
        /// Last-used index for round-robin load balancing.
        /// </summary>
        public int LastIndex { get; set; } = 0;

        /// <summary>
        /// Total requests processed by this endpoint.
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

        #endregion

        #region Private-Members

        private string _Identifier = string.Empty;
        private long _TotalRequestsInternal = 0;
        private long _SuccessfulRequestsInternal = 0;
        private long _FailedRequestsInternal = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ApiEndpointState()
        {
        }

        /// <summary>
        /// Instantiate with identifier.
        /// </summary>
        /// <param name="identifier">Endpoint identifier.</param>
        public ApiEndpointState(string identifier)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        }

        /// <summary>
        /// Create state from configuration.
        /// </summary>
        /// <param name="config">API endpoint configuration.</param>
        /// <returns>New state object.</returns>
        public static ApiEndpointState FromConfig(ApiEndpointConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return new ApiEndpointState(config.Identifier);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Get and increment the last index atomically for round-robin.
        /// </summary>
        /// <param name="maxValue">Maximum value (number of origins).</param>
        /// <returns>Next index to use.</returns>
        public int GetNextIndex(int maxValue)
        {
            if (maxValue <= 0) return 0;

            lock (Lock)
            {
                int index = LastIndex;
                LastIndex = (LastIndex + 1) % maxValue;
                return index;
            }
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
        /// Increment successful request count atomically.
        /// </summary>
        /// <returns>New count.</returns>
        public long IncrementSuccessfulRequests()
        {
            return Interlocked.Increment(ref _SuccessfulRequestsInternal);
        }

        /// <summary>
        /// Increment failed request count atomically.
        /// </summary>
        /// <returns>New count.</returns>
        public long IncrementFailedRequests()
        {
            return Interlocked.Increment(ref _FailedRequestsInternal);
        }

        #endregion
    }
}
