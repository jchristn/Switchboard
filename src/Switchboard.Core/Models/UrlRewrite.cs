#nullable enable

namespace Switchboard.Core.Models
{
    using System;

    /// <summary>
    /// URL rewrite rule for an API endpoint.
    /// </summary>
    public class UrlRewrite
    {
        #region Public-Members

        /// <summary>
        /// Auto-incremented primary key.
        /// </summary>
        public int Id { get; set; } = 0;

        /// <summary>
        /// Endpoint identifier (foreign key).
        /// </summary>
        public string EndpointIdentifier
        {
            get => _EndpointIdentifier;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(EndpointIdentifier));
                _EndpointIdentifier = value;
            }
        }

        /// <summary>
        /// HTTP method this rewrite applies to.
        /// </summary>
        public string HttpMethod
        {
            get => _HttpMethod;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(HttpMethod));
                _HttpMethod = value.ToUpperInvariant();
            }
        }

        /// <summary>
        /// Source URL pattern to match.
        /// </summary>
        public string SourcePattern
        {
            get => _SourcePattern;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(SourcePattern));
                _SourcePattern = value;
            }
        }

        /// <summary>
        /// Target URL pattern to rewrite to.
        /// </summary>
        public string TargetPattern
        {
            get => _TargetPattern;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(TargetPattern));
                _TargetPattern = value;
            }
        }

        /// <summary>
        /// Sort order for rewrite rule priority.
        /// Lower values are applied first.
        /// </summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Timestamp when this record was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Endpoint GUID (foreign key).
        /// </summary>
        public Guid EndpointGUID { get; set; } = Guid.Empty;

        #endregion

        #region Private-Members

        private string _EndpointIdentifier = string.Empty;
        private string _HttpMethod = "GET";
        private string _SourcePattern = string.Empty;
        private string _TargetPattern = string.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public UrlRewrite()
        {
        }

        /// <summary>
        /// Instantiate with parameters.
        /// </summary>
        /// <param name="endpointIdentifier">Endpoint identifier.</param>
        /// <param name="httpMethod">HTTP method.</param>
        /// <param name="sourcePattern">Source URL pattern.</param>
        /// <param name="targetPattern">Target URL pattern.</param>
        public UrlRewrite(string endpointIdentifier, string httpMethod, string sourcePattern, string targetPattern)
        {
            EndpointIdentifier = endpointIdentifier ?? throw new ArgumentNullException(nameof(endpointIdentifier));
            HttpMethod = httpMethod ?? throw new ArgumentNullException(nameof(httpMethod));
            SourcePattern = sourcePattern ?? throw new ArgumentNullException(nameof(sourcePattern));
            TargetPattern = targetPattern ?? throw new ArgumentNullException(nameof(targetPattern));
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
