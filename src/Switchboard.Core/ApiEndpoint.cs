﻿namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;

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
        /// HTTP method.
        /// </summary>
        public HttpMethod Method { get; set; } = HttpMethod.GET;
        
        /// <summary>
        /// Parameterized URL to match, e.g. /{version}/foo/bar/{id}.
        /// </summary>
        public string ParameterizedUrl { get; set; } = null;

        /// <summary>
        /// URL prefix to match.
        /// </summary>
        public string UrlPrefix { get; set; } = null;

        /// <summary>
        /// Number of milliseconds to wait before considering the request to be timed out.
        /// Zero indicates no timeout.
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
        /// Rate limit interval.
        /// </summary>
        public RateLimitIntervalEnum RateLimitInterval { get; set; } = RateLimitIntervalEnum.Minutes;

        /// <summary>
        /// Rate limit per requesting endpoint, within the specified RateLimitInterval.  
        /// Zero indicates no rate limit.
        /// </summary>
        public int RateLimit
        {
            get
            {
                return _RateLimit;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(RateLimit));
                _RateLimit = value;
            }
        }

        /// <summary>
        /// Load-balancing mode.
        /// </summary>
        public LoadBalancingMode LoadBalancing { get; set; } = LoadBalancingMode.RoundRobin;

        /// <summary>
        /// True to enable sticky sessions.
        /// </summary>
        public bool StickySessions { get; set; } = false;

        /// <summary>
        /// Enable or disable retries should a backend server return a non-success status code.
        /// </summary>
        public bool EnableRetries { get; set; } = true;

        /// <summary>
        /// Retry count.
        /// </summary>
        public int RetryCount
        {
            get
            {
                return _RetryCount;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(RetryCount));
                _RetryCount = value;
            }
        }

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

        #endregion

        #region Private-Members

        private int _TimeoutMs = 60000;
        private int _RateLimit = 100;
        private int _RetryCount = 3;
        private int _MaxRequestBodySize = (512 * 1024 * 1024);
        private List<string> _OriginServers = new List<string>();
        private int _LastIndex = 0;
        private List<string> _BlockedHeaders = new List<string>();

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
