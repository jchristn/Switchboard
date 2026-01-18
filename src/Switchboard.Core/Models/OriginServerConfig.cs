#nullable enable

namespace Switchboard.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Origin server configuration.
    /// This class contains only configuration data that is stored in the database.
    /// Runtime state is maintained separately in OriginServerState.
    /// </summary>
    public class OriginServerConfig
    {
        #region Public-Members

        /// <summary>
        /// Unique GUID for this origin server.
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Unique identifier for this origin server.
        /// Primary key.
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
        /// Display name for this origin server.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Hostname.
        /// </summary>
        public string Hostname
        {
            get => _Hostname;
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
            get => _Port;
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
        /// Default is 5 seconds (5000). Minimum is 1 second (1000).
        /// </summary>
        public int HealthCheckIntervalMs
        {
            get => _HealthCheckIntervalMs;
            set
            {
                if (value < 1000) throw new ArgumentOutOfRangeException(nameof(HealthCheckIntervalMs));
                _HealthCheckIntervalMs = value;
            }
        }

        /// <summary>
        /// HTTP method to use when performing a healthcheck.
        /// Default is HEAD.
        /// </summary>
        public string HealthCheckMethod
        {
            get => _HealthCheckMethod;
            set
            {
                if (String.IsNullOrEmpty(value)) value = "HEAD";
                _HealthCheckMethod = value.ToUpperInvariant();
            }
        }

        /// <summary>
        /// URL to use when performing a healthcheck.
        /// Default is /.
        /// </summary>
        public string HealthCheckUrl
        {
            get => _HealthCheckUrl;
            set
            {
                if (String.IsNullOrEmpty(value)) value = "/";
                _HealthCheckUrl = value;
            }
        }

        /// <summary>
        /// Number of consecutive failed health checks before marking an origin as unhealthy.
        /// Default is 2.
        /// </summary>
        public int UnhealthyThreshold
        {
            get => _UnhealthyThreshold;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(UnhealthyThreshold));
                _UnhealthyThreshold = value;
            }
        }

        /// <summary>
        /// Number of consecutive successful health checks before marking an origin as healthy.
        /// Default is 1.
        /// </summary>
        public int HealthyThreshold
        {
            get => _HealthyThreshold;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(HealthyThreshold));
                _HealthyThreshold = value;
            }
        }

        /// <summary>
        /// Maximum number of parallel requests to this backend.
        /// Default is 10.
        /// </summary>
        public int MaxParallelRequests
        {
            get => _MaxParallelRequests;
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
            get => _RateLimitRequestsThreshold;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(RateLimitRequestsThreshold));
                _RateLimitRequestsThreshold = value;
            }
        }

        /// <summary>
        /// True to log requests to this origin.
        /// </summary>
        public bool LogRequest { get; set; } = false;

        /// <summary>
        /// True to log the request body.
        /// </summary>
        public bool LogRequestBody { get; set; } = false;

        /// <summary>
        /// True to log responses from this origin.
        /// </summary>
        public bool LogResponse { get; set; } = false;

        /// <summary>
        /// True to log the response body.
        /// </summary>
        public bool LogResponseBody { get; set; } = false;

        /// <summary>
        /// Enable capture of request body for this origin.
        /// Default is false.
        /// </summary>
        public bool CaptureRequestBody { get; set; } = false;

        /// <summary>
        /// Enable capture of response body for this origin.
        /// Default is false.
        /// </summary>
        public bool CaptureResponseBody { get; set; } = false;

        /// <summary>
        /// Enable capture of request headers for this origin.
        /// Default is true.
        /// </summary>
        public bool CaptureRequestHeaders { get; set; } = true;

        /// <summary>
        /// Enable capture of response headers for this origin.
        /// Default is true.
        /// </summary>
        public bool CaptureResponseHeaders { get; set; } = true;

        /// <summary>
        /// Maximum request body size to capture in bytes.
        /// Default is 64KB.
        /// </summary>
        public int MaxCaptureRequestBodySize
        {
            get => _MaxCaptureRequestBodySize;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(MaxCaptureRequestBodySize));
                _MaxCaptureRequestBodySize = value;
            }
        }

        /// <summary>
        /// Maximum response body size to capture in bytes.
        /// Default is 64KB.
        /// </summary>
        public int MaxCaptureResponseBodySize
        {
            get => _MaxCaptureResponseBodySize;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(MaxCaptureResponseBodySize));
                _MaxCaptureResponseBodySize = value;
            }
        }

        /// <summary>
        /// Timestamp when this record was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when this record was last modified.
        /// </summary>
        public DateTime? ModifiedUtc { get; set; } = null;

        /// <summary>
        /// URL prefix for this origin server.
        /// </summary>
        [JsonIgnore]
        public string UrlPrefix => (Ssl ? "https://" : "http://") + Hostname + ":" + Port;

        #endregion

        #region Private-Members

        private string _Identifier = Guid.NewGuid().ToString();
        private string _Hostname = "localhost";
        private int _Port = 8000;
        private int _HealthCheckIntervalMs = 5000;
        private string _HealthCheckMethod = "HEAD";
        private string _HealthCheckUrl = "/";
        private int _UnhealthyThreshold = 2;
        private int _HealthyThreshold = 1;
        private int _MaxParallelRequests = 10;
        private int _RateLimitRequestsThreshold = 30;
        private int _MaxCaptureRequestBodySize = 65536;
        private int _MaxCaptureResponseBodySize = 65536;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OriginServerConfig()
        {
        }

        /// <summary>
        /// Instantiate with identifier.
        /// </summary>
        /// <param name="identifier">Unique identifier.</param>
        public OriginServerConfig(string identifier)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
