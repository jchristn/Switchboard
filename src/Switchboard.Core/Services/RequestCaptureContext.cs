#nullable enable

namespace Switchboard.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// Context for capturing a single request.
    /// Holds all data collected during the request lifecycle for history capture.
    /// </summary>
    public class RequestCaptureContext
    {
        #region Public-Members

        /// <summary>
        /// Request ID.
        /// </summary>
        public Guid RequestId { get; }

        /// <summary>
        /// Start time of the request.
        /// </summary>
        public DateTime StartTimeUtc { get; }

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string? HttpMethod { get; set; }

        /// <summary>
        /// Request path.
        /// </summary>
        public string? RequestPath { get; set; }

        /// <summary>
        /// Query string.
        /// </summary>
        public string? QueryString { get; set; }

        /// <summary>
        /// Matched endpoint identifier.
        /// </summary>
        public string? EndpointIdentifier { get; set; }

        /// <summary>
        /// Matched endpoint GUID.
        /// </summary>
        public Guid? EndpointGuid { get; set; }

        /// <summary>
        /// Selected origin identifier.
        /// </summary>
        public string? OriginIdentifier { get; set; }

        /// <summary>
        /// Selected origin GUID.
        /// </summary>
        public Guid? OriginGuid { get; set; }

        /// <summary>
        /// Client IP address.
        /// </summary>
        public string? ClientIp { get; set; }

        /// <summary>
        /// Request headers.
        /// </summary>
        public Dictionary<string, string>? RequestHeaders { get; set; }

        /// <summary>
        /// Request body.
        /// </summary>
        public string? RequestBody { get; set; }

        /// <summary>
        /// Request body size in bytes.
        /// </summary>
        public long RequestBodySize { get; set; }

        /// <summary>
        /// Response status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Response headers.
        /// </summary>
        public Dictionary<string, string>? ResponseHeaders { get; set; }

        /// <summary>
        /// Response body.
        /// </summary>
        public string? ResponseBody { get; set; }

        /// <summary>
        /// Response body size in bytes.
        /// </summary>
        public long ResponseBodySize { get; set; }

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public long DurationMs { get; private set; }

        /// <summary>
        /// Whether the request was authenticated.
        /// </summary>
        public bool WasAuthenticated { get; set; }

        /// <summary>
        /// Error message if the request failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Endpoint for per-endpoint capture settings.
        /// </summary>
        public ApiEndpoint? Endpoint { get; set; }

        /// <summary>
        /// Origin server for per-origin capture settings.
        /// </summary>
        public OriginServer? Origin { get; set; }

        #endregion

        #region Private-Members

        private readonly Stopwatch _Stopwatch;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate request capture context.
        /// </summary>
        /// <param name="requestId">Request ID.</param>
        public RequestCaptureContext(Guid requestId)
        {
            RequestId = requestId;
            StartTimeUtc = DateTime.UtcNow;
            _Stopwatch = Stopwatch.StartNew();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Complete the capture and record duration.
        /// </summary>
        public void Complete()
        {
            _Stopwatch.Stop();
            DurationMs = _Stopwatch.ElapsedMilliseconds;
        }

        #endregion
    }
}
