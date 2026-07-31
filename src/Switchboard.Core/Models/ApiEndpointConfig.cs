#nullable enable

namespace Switchboard.Core.Models
{
    using System;
    using Switchboard.Core;

    /// <summary>
    /// API endpoint configuration.
    /// This class contains only configuration data that is stored in the database.
    /// Runtime state is maintained separately in ApiEndpointState.
    /// Routes and origin mappings are stored in separate tables.
    /// </summary>
    public class ApiEndpointConfig
    {
        #region Public-Members

        /// <summary>
        /// Unique GUID for this API endpoint.
        /// Derived deterministically from <see cref="Identifier"/> so it is stable across reads; the
        /// database keys endpoints by identifier and does not persist a GUID column.
        /// </summary>
        public Guid GUID => DeterministicGuid.FromString(Identifier);

        /// <summary>
        /// Unique identifier for this API endpoint.
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
        /// Display name for this API endpoint.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Number of milliseconds to wait before considering the request to be timed out.
        /// Default is 60 seconds.
        /// </summary>
        public int TimeoutMs
        {
            get => _TimeoutMs;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(TimeoutMs));
                _TimeoutMs = value;
            }
        }

        /// <summary>
        /// Load-balancing mode.
        /// </summary>
        public string LoadBalancingMode
        {
            get => _LoadBalancingMode;
            set
            {
                if (String.IsNullOrEmpty(value)) value = "RoundRobin";
                _LoadBalancingMode = value;
            }
        }

        /// <summary>
        /// True to terminate HTTP/1.0 requests.
        /// </summary>
        public bool BlockHttp10 { get; set; } = false;

        /// <summary>
        /// Maximum request body size in bytes.
        /// Default is 512MB.
        /// </summary>
        public int MaxRequestBodySize
        {
            get => _MaxRequestBodySize;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(MaxRequestBodySize));
                _MaxRequestBodySize = value;
            }
        }

        /// <summary>
        /// True to enable logging of the full request.
        /// </summary>
        public bool LogRequestFull { get; set; } = false;

        /// <summary>
        /// True to log the request body.
        /// </summary>
        public bool LogRequestBody { get; set; } = false;

        /// <summary>
        /// True to log the response body.
        /// </summary>
        public bool LogResponseBody { get; set; } = false;

        /// <summary>
        /// Boolean indicating whether or not the auth context header should be included for authenticated requests.
        /// </summary>
        public bool IncludeAuthContextHeader { get; set; } = true;

        /// <summary>
        /// Header to add when passing authentication context to an origin server.
        /// </summary>
        public string? AuthContextHeader { get; set; } = "x-sb-auth-context";

        /// <summary>
        /// True to use global blocked headers.
        /// </summary>
        public bool UseGlobalBlockedHeaders { get; set; } = true;

        /// <summary>
        /// True to enable sticky sessions (session affinity). When enabled, requests are consistently
        /// hashed to the same origin using the value of <see cref="StickySessionHeader"/> when set, or the
        /// client IP address otherwise, so a given client is pinned to one origin regardless of the base
        /// load-balancing mode. Default is false.
        /// </summary>
        public bool StickySessionEnabled { get; set; } = false;

        /// <summary>
        /// Name of the request header whose value is used as the sticky-session affinity key. When null
        /// (the default) and sticky sessions are enabled, the client IP address is used instead.
        /// </summary>
        public string? StickySessionHeader { get; set; } = null;

        /// <summary>
        /// Maximum number of additional origins to try after the first failed attempt (transport error or,
        /// when <see cref="RetryOn5xx"/> is enabled, an upstream 5xx) before returning an error. Retries
        /// apply only to idempotent request methods and only while no response bytes have been sent to the
        /// client. Default is 0 (no retries). Minimum is 0. Maximum is 10. Values are clamped into range.
        /// </summary>
        public int MaxRetries
        {
            get => _MaxRetries;
            set
            {
                if (value < 0) value = 0;
                if (value > 10) value = 10;
                _MaxRetries = value;
            }
        }

        /// <summary>
        /// True to treat an upstream 5xx response as a retryable failure (in addition to transport errors)
        /// when <see cref="MaxRetries"/> is greater than 0. Default is true.
        /// </summary>
        public bool RetryOn5xx { get; set; } = true;

        /// <summary>
        /// Enable capture of request body for this endpoint.
        /// Default is false.
        /// </summary>
        public bool CaptureRequestBody { get; set; } = false;

        /// <summary>
        /// Enable capture of response body for this endpoint.
        /// Default is false.
        /// </summary>
        public bool CaptureResponseBody { get; set; } = false;

        /// <summary>
        /// Enable capture of request headers for this endpoint.
        /// Default is true.
        /// </summary>
        public bool CaptureRequestHeaders { get; set; } = true;

        /// <summary>
        /// Enable capture of response headers for this endpoint.
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

        #endregion

        #region Private-Members

        private string _Identifier = Guid.NewGuid().ToString();
        private int _TimeoutMs = 60000;
        private string _LoadBalancingMode = "RoundRobin";
        private int _MaxRequestBodySize = (512 * 1024 * 1024);
        private int _MaxRetries = 0;
        private int _MaxCaptureRequestBodySize = 65536;
        private int _MaxCaptureResponseBodySize = 65536;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ApiEndpointConfig()
        {
        }

        /// <summary>
        /// Instantiate with identifier.
        /// </summary>
        /// <param name="identifier">Unique identifier.</param>
        public ApiEndpointConfig(string identifier)
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
