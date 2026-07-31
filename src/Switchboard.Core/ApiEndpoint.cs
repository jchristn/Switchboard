namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Switchboard.Core.Models;

    /// <summary>
    /// API endpoint.
    /// </summary>
    public class ApiEndpoint
    {
        #region Public-Members

        /// <summary>
        /// Unique identifier for this API endpoint.
        /// </summary>
        public string Identifier { get; set; } = null;

        /// <summary>
        /// Name for this API endpoint.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Number of milliseconds to wait before considering the request to be timed out.
        /// Default is 60 seconds.
        /// </summary>
        public int TimeoutMs
        {
            get
            {
                return _TimeoutMs;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(TimeoutMs));
                _TimeoutMs = value;
            }
        }

        /// <summary>
        /// Load-balancing mode.
        /// </summary>
        public LoadBalancingMode LoadBalancing { get; set; } = LoadBalancingMode.RoundRobin;

        /// <summary>
        /// True to enable sticky sessions (session affinity). When enabled, requests are consistently
        /// hashed to the same origin using <see cref="StickySessionHeader"/> when set, or the client IP
        /// address otherwise, overriding the base load-balancing mode. Default is false.
        /// </summary>
        public bool StickySessionEnabled { get; set; } = false;

        /// <summary>
        /// Name of the request header whose value is the sticky-session affinity key. When null (the
        /// default) and sticky sessions are enabled, the client IP address is used instead.
        /// </summary>
        public string StickySessionHeader { get; set; } = null;

        /// <summary>
        /// Maximum number of additional origins to try after a failed attempt (transport error or, when
        /// <see cref="RetryOn5xx"/> is enabled, an upstream 5xx) before returning an error. Retries apply
        /// only to idempotent methods and only while no response bytes have been sent. Default is 0.
        /// Minimum is 0. Maximum is 10. Values are clamped into range.
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
        /// True to treat an upstream 5xx response as a retryable failure when <see cref="MaxRetries"/> is
        /// greater than 0. Default is true.
        /// </summary>
        public bool RetryOn5xx { get; set; } = true;

        /// <summary>
        /// Boolean indicating whether or not the auth context header should be included for authenticated requests.
        /// </summary>
        public bool IncludeAuthContextHeader { get; set; } = true;

        /// <summary>
        /// True to terminate HTTP/1.0 requests.
        /// </summary>
        public bool BlockHttp10 { get; set; } = false;

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
        /// Maximum request body size.  Default is 512MB.
        /// </summary>
        public int MaxRequestBodySize
        {
            get
            {
                return _MaxRequestBodySize;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(MaxRequestBodySize));
                _MaxRequestBodySize = value;
            }
        }

        /// <summary>
        /// Origin server identifiers.
        /// </summary>
        public List<string> OriginServers
        {
            get
            {
                return _OriginServers;
            }
            set
            {
                if (value == null) value = new List<string>();
                _OriginServers = value;
            }
        }

        /// <summary>
        /// Per-endpoint origin bindings carrying weight, priority tier, and canary header match for each
        /// origin. Populated from endpoint-origin mappings for database-managed endpoints. When empty (for
        /// example, file-configured endpoints), the load balancer treats every entry in
        /// <see cref="OriginServers"/> as a default binding (weight 100, priority 0, no canary).
        /// </summary>
        [JsonIgnore]
        public List<OriginBinding> OriginBindings
        {
            get
            {
                return _OriginBindings;
            }
            set
            {
                if (value == null) value = new List<OriginBinding>();
                _OriginBindings = value;
            }
        }

        /// <summary>
        /// Last-used index.
        /// </summary>
        [JsonIgnore]
        public int LastIndex
        {
            get
            {
                return _LastIndex;
            }
            set
            {
                if (value < 0 || value > (_OriginServers.Count - 1)) throw new ArgumentOutOfRangeException(nameof(LastIndex));
                _LastIndex = value;
            }
        }

        /// <summary>
        /// True to use global blocked headers.  Headers in the global blocked headers will not be forwarded from incoming requests to origin servers.
        /// </summary>
        public bool UseGlobalBlockedHeaders { get; set; } = true;

        /// <summary>
        /// Header to add when passing authentication context to an origin server.  
        /// When set, the entire AuthenticationResult object will be JSON serialized and base64 encoded, and passed to the origin server using this header.
        /// </summary>
        public string AuthContextHeader { get; set; } = Constants.AuthContextHeader;

        /// <summary>
        /// Explicit list of blocked headers.  These headers are not forwarded from incoming requests to origin servers.
        /// </summary>
        public List<string> BlockedHeaders
        {
            get
            {
                return _BlockedHeaders;
            }
            set
            {
                if (value == null) value = new List<string>();
                _BlockedHeaders = value;
            }
        }

        /// <summary>
        /// Unauthenticated API endpoints.
        /// </summary>
        public ApiEndpointGroup Unauthenticated
        {
            get
            {
                return _Unauthenticated;
            }
            set
            {
                if (value == null) value = new ApiEndpointGroup();
                _Unauthenticated = value;
            }
        }

        /// <summary>
        /// Authenticated API endpoints.
        /// </summary>
        public ApiEndpointGroup Authenticated
        {
            get
            {
                return _Authenticated;
            }
            set
            {
                if (value == null) value = new ApiEndpointGroup();
                _Authenticated = value;
            }
        }

        /// <summary>
        /// Key is the upper-case HTTP method.
        /// Value is a dictionary where the key is the original URL and the value is the URL to which the request should be directed.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> RewriteUrls
        {
            get
            {
                return _RewriteUrls;
            }
            set
            {
                if (value == null) value = new Dictionary<string, Dictionary<string, string>>();
                _RewriteUrls = value;
            }
        }

        /// <summary>
        /// OpenAPI documentation metadata for routes in this endpoint.
        /// Optional - if not provided, minimal documentation is auto-generated.
        /// </summary>
        public OpenApiEndpointMetadata OpenApiDocumentation
        {
            get
            {
                return _OpenApiDocumentation;
            }
            set
            {
                if (value == null) value = new OpenApiEndpointMetadata();
                _OpenApiDocumentation = value;
            }
        }

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

        #endregion

        #region Internal-Members

        internal readonly object Lock = new object();

        // Round-robin cursor used by the load balancer. Unvalidated (unlike LastIndex) so it can advance
        // over a filtered candidate subset without the range validation on LastIndex; guarded by Lock.
        internal int RoundRobinIndex = 0;

        #endregion

        #region Private-Members

        private int _TimeoutMs = 60000;
        private int _MaxRequestBodySize = (512 * 1024 * 1024);
        private int _MaxRetries = 0;
        private List<string> _OriginServers = new List<string>();
        private List<OriginBinding> _OriginBindings = new List<OriginBinding>();
        private int _LastIndex = 0;
        private List<string> _BlockedHeaders = new List<string>();
        private ApiEndpointGroup _Unauthenticated = new ApiEndpointGroup();
        private ApiEndpointGroup _Authenticated = new ApiEndpointGroup();
        private Dictionary<string, Dictionary<string, string>> _RewriteUrls = new Dictionary<string, Dictionary<string, string>>();
        private OpenApiEndpointMetadata _OpenApiDocumentation = new OpenApiEndpointMetadata();
        private int _MaxCaptureRequestBodySize = 65536;
        private int _MaxCaptureResponseBodySize = 65536;

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ApiEndpoint()
        {

        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
