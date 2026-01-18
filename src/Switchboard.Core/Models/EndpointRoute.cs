#nullable enable

namespace Switchboard.Core.Models
{
    using System;

    /// <summary>
    /// URL route for an API endpoint.
    /// Defines HTTP method and URL pattern that maps to an endpoint.
    /// </summary>
    public class EndpointRoute
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
        /// HTTP method (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS).
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
        /// URL pattern (parameterized URL, e.g., /api/users/{id}).
        /// </summary>
        public string UrlPattern
        {
            get => _UrlPattern;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(UrlPattern));
                _UrlPattern = value;
            }
        }

        /// <summary>
        /// True if this route requires authentication.
        /// </summary>
        public bool RequiresAuthentication { get; set; } = false;

        /// <summary>
        /// Sort order for route matching priority.
        /// Lower values are matched first.
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
        private string _UrlPattern = "/";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EndpointRoute()
        {
        }

        /// <summary>
        /// Instantiate with parameters.
        /// </summary>
        /// <param name="endpointIdentifier">Endpoint identifier.</param>
        /// <param name="httpMethod">HTTP method.</param>
        /// <param name="urlPattern">URL pattern.</param>
        /// <param name="requiresAuthentication">True if authentication is required.</param>
        public EndpointRoute(string endpointIdentifier, string httpMethod, string urlPattern, bool requiresAuthentication = false)
        {
            EndpointIdentifier = endpointIdentifier ?? throw new ArgumentNullException(nameof(endpointIdentifier));
            HttpMethod = httpMethod ?? throw new ArgumentNullException(nameof(httpMethod));
            UrlPattern = urlPattern ?? throw new ArgumentNullException(nameof(urlPattern));
            RequiresAuthentication = requiresAuthentication;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
