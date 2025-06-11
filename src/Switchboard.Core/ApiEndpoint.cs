namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

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

        #endregion

        #region Internal-Members

        internal readonly object Lock = new object();

        #endregion

        #region Private-Members

        private int _TimeoutMs = 60000;
        private int _MaxRequestBodySize = (512 * 1024 * 1024);
        private List<string> _OriginServers = new List<string>();
        private int _LastIndex = 0;
        private List<string> _BlockedHeaders = new List<string>();
        private ApiEndpointGroup _Unauthenticated = new ApiEndpointGroup();
        private ApiEndpointGroup _Authenticated = new ApiEndpointGroup();
        private Dictionary<string, Dictionary<string, string>> _RewriteUrls = new Dictionary<string, Dictionary<string, string>>();

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
