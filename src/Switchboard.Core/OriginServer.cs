namespace Switchboard.Core
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;

    /// <summary>
    /// Origin server.
    /// </summary>
    public class OriginServer
    {
        #region Public-Members

        /// <summary>
        /// Unique identifier for this origin server.
        /// </summary>
        public string Identifier { get; set; } = null;

        /// <summary>
        /// Name for this origin server.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Hostname.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// TCP port.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 0 || value > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
                _Port = value;
            }
        }

        /// <summary>
        /// Enable or disable SSL.
        /// </summary>
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// Interval at which health is checked against this origin.
        /// Default is 5 seconds (5000).  Minimum is 1 second (1000).
        /// </summary>
        public int HealthCheckIntervalMs
        {
            get
            {
                return _HealthCheckIntervalMs;
            }
            set
            {
                if (value < 1000) throw new ArgumentOutOfRangeException(nameof(HealthCheckIntervalMs));
                _HealthCheckIntervalMs = value;
            }
        }

        /// <summary>
        /// Number of consecutive failed health checks before marking an origin as unhealthy.
        /// </summary>
        public int UnhealthyThreshold
        {
            get
            {
                return _UnhealthyThreshold;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(UnhealthyThreshold));
                _UnhealthyThreshold = value;
            }
        }

        /// <summary>
        /// Number of consecutive successful health checks before marking an origin as healthy.
        /// </summary>
        public int HealthyThreshold
        {
            get
            {
                return _HealthyThreshold;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(HealthyThreshold));
                _HealthyThreshold = value;
            }
        }

        /// <summary>
        /// HTTP method to use when performing a healthcheck.
        /// Default is GET.
        /// </summary>
        public HttpMethod HealthCheckMethod
        {
            get
            {
                return _HealthCheckMethod;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(HealthCheckMethod));
                _HealthCheckMethod = value;
            }
        }

        /// <summary>
        /// URL to use when performing a healthcheck.
        /// Default is /.
        /// </summary>
        public string HealthCheckUrl
        {
            get
            {
                return _HealthCheckUrl;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) value = "/";
                _HealthCheckUrl = value;
            }
        }

        /// <summary>
        /// Boolean indicating if the backend is healthy.
        /// </summary>
        [JsonIgnore]
        public bool Healthy { get; internal set; } = false;

        /// <summary>
        /// Maximum number of parallel requests to this backend.
        /// Default is 10.
        /// </summary>
        public int MaxParallelRequests
        {
            get
            {
                return _MaxParallelRequests;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(MaxParallelRequests));
                _MaxParallelRequests = value;
            }
        }

        /// <summary>
        /// Threshold at which 429 rate limit responses are sent.
        /// Default is 30.
        /// </summary>
        public int RateLimitRequestsThreshold
        {
            get
            {
                return _RateLimitRequestsThreshold;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(RateLimitRequestsThreshold));
                _RateLimitRequestsThreshold = value;
            }
        }

        /// <summary>
        /// True to log the request body.
        /// </summary>
        public bool LogRequestBody { get; set; } = false;

        /// <summary>
        /// True to log the response body.
        /// </summary>
        public bool LogResponseBody { get; set; } = false;

        /// <summary>
        /// URL.
        /// </summary>
        [JsonIgnore]
        public string UrlPrefix
        {
            get
            {
                return (Ssl ? "https://" : "http://") + Hostname + ":" + Port;
            }
        }

        #endregion

        #region Internal-Members

        internal readonly object Lock = new object();
        internal int HealthCheckSuccess = 0;
        internal int HealthCheckFailure = 0;
        internal bool ModelsDiscovered = false;
        internal int ActiveRequests = 0;
        internal int PendingRequests = 0;
        internal SemaphoreSlim Semaphore
        {
            get
            {
                if (_Semaphore == null) _Semaphore = new SemaphoreSlim(_MaxParallelRequests, _MaxParallelRequests);
                return _Semaphore;
            }
            set
            {
                _Semaphore = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 8000;
        private int _HealthCheckIntervalMs = 5000;
        private int _UnhealthyThreshold = 2;
        private int _HealthyThreshold = 2;
        private int _MaxParallelRequests = 10;
        private int _RateLimitRequestsThreshold = 30;
        private HttpMethod _HealthCheckMethod = HttpMethod.Get;
        private string _HealthCheckUrl = "/";
        private SemaphoreSlim _Semaphore = null;

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
