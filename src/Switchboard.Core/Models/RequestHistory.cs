#nullable enable

namespace Switchboard.Core.Models
{
    using System;

    /// <summary>
    /// Request history record.
    /// Stores information about processed requests for monitoring and debugging.
    /// </summary>
    public class RequestHistory
    {
        #region Public-Members

        /// <summary>
        /// Auto-increment ID.
        /// </summary>
        public long Id { get; set; } = 0;

        /// <summary>
        /// Unique GUID for this request.
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Unique identifier for this request.
        /// Primary key.
        /// </summary>
        public Guid RequestId
        {
            get => _RequestId;
            set
            {
                if (value == Guid.Empty) throw new ArgumentException("RequestId cannot be empty.", nameof(RequestId));
                _RequestId = value;
            }
        }

        /// <summary>
        /// Timestamp when the request was received.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// HTTP method of the request.
        /// </summary>
        public string HttpMethod
        {
            get => _HttpMethod;
            set
            {
                if (String.IsNullOrEmpty(value)) value = "GET";
                _HttpMethod = value.ToUpperInvariant();
            }
        }

        /// <summary>
        /// Request path (without query string).
        /// </summary>
        public string RequestPath
        {
            get => _RequestPath;
            set
            {
                if (String.IsNullOrEmpty(value)) value = "/";
                _RequestPath = value;
            }
        }

        /// <summary>
        /// Query string (without leading ?).
        /// </summary>
        public string? QueryString { get; set; } = null;

        /// <summary>
        /// Matched endpoint identifier.
        /// </summary>
        public string? EndpointIdentifier { get; set; } = null;

        /// <summary>
        /// Matched endpoint GUID.
        /// </summary>
        public Guid? EndpointGUID { get; set; } = null;

        /// <summary>
        /// Selected origin server identifier.
        /// </summary>
        public string? OriginIdentifier { get; set; } = null;

        /// <summary>
        /// Selected origin server GUID.
        /// </summary>
        public Guid? OriginGUID { get; set; } = null;

        /// <summary>
        /// Client IP address.
        /// </summary>
        public string? ClientIp { get; set; } = null;

        /// <summary>
        /// Size of the request body in bytes.
        /// </summary>
        public long RequestBodySize { get; set; } = 0;

        /// <summary>
        /// Request body content (if captured).
        /// </summary>
        public string? RequestBody { get; set; } = null;

        /// <summary>
        /// Request headers as JSON.
        /// </summary>
        public string? RequestHeaders { get; set; } = null;

        /// <summary>
        /// Response status code.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Size of the response body in bytes.
        /// </summary>
        public long ResponseBodySize { get; set; } = 0;

        /// <summary>
        /// Response body content (if captured).
        /// </summary>
        public string? ResponseBody { get; set; } = null;

        /// <summary>
        /// Response headers as JSON.
        /// </summary>
        public string? ResponseHeaders { get; set; } = null;

        /// <summary>
        /// Total request duration in milliseconds.
        /// </summary>
        public long DurationMs { get; set; } = 0;

        /// <summary>
        /// True if the request was authenticated.
        /// </summary>
        public bool WasAuthenticated { get; set; } = false;

        /// <summary>
        /// Error message if the request failed.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// True if the request was successful (2xx or 3xx status code).
        /// </summary>
        public bool Success { get; set; } = false;

        #endregion

        #region Private-Members

        private Guid _RequestId = Guid.NewGuid();
        private string _HttpMethod = "GET";
        private string _RequestPath = "/";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestHistory()
        {
        }

        /// <summary>
        /// Instantiate with request ID.
        /// </summary>
        /// <param name="requestId">Request ID.</param>
        public RequestHistory(Guid requestId)
        {
            RequestId = requestId;
        }

        #endregion
    }
}
