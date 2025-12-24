namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// OpenAPI metadata for an API endpoint.
    /// Contains route documentation organized by HTTP method and URL pattern.
    /// </summary>
    public class OpenApiEndpointMetadata
    {
        #region Public-Members

        /// <summary>
        /// Route documentation by HTTP method and URL pattern.
        /// Key: HTTP method (GET, POST, etc.)
        /// Value: Dictionary of URL pattern to route documentation.
        /// </summary>
        public Dictionary<string, Dictionary<string, OpenApiRouteDocumentation>> Routes
        {
            get
            {
                return _Routes;
            }
            set
            {
                if (value == null) value = new Dictionary<string, Dictionary<string, OpenApiRouteDocumentation>>();
                _Routes = value;
            }
        }

        #endregion

        #region Private-Members

        private Dictionary<string, Dictionary<string, OpenApiRouteDocumentation>> _Routes =
            new Dictionary<string, Dictionary<string, OpenApiRouteDocumentation>>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OpenApiEndpointMetadata()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Get documentation for a specific route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="urlPattern">URL pattern.</param>
        /// <returns>Route documentation or null if not found.</returns>
        public OpenApiRouteDocumentation GetDocumentation(string method, string urlPattern)
        {
            if (String.IsNullOrEmpty(method) || String.IsNullOrEmpty(urlPattern))
                return null;

            method = method.ToUpperInvariant();

            if (_Routes.TryGetValue(method, out Dictionary<string, OpenApiRouteDocumentation> methodRoutes))
            {
                if (methodRoutes.TryGetValue(urlPattern, out OpenApiRouteDocumentation doc))
                {
                    return doc;
                }
            }

            return null;
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
