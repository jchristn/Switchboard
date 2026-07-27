#nullable enable

namespace Switchboard.Core.Models
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Optional request body for the configuration validation endpoint. When supplied with any
    /// content, the posted configuration is validated instead of the configuration currently stored
    /// in the database.
    /// </summary>
    public class ConfigValidationRequest
    {
        #region Public-Members

        /// <summary>
        /// API endpoints to validate.
        /// </summary>
        public List<ApiEndpointConfig>? Endpoints { get; set; } = null;

        /// <summary>
        /// Origin servers to validate.
        /// </summary>
        public List<OriginServerConfig>? Origins { get; set; } = null;

        /// <summary>
        /// Endpoint routes to validate.
        /// </summary>
        public List<EndpointRoute>? Routes { get; set; } = null;

        /// <summary>
        /// Endpoint-to-origin mappings to validate.
        /// </summary>
        public List<EndpointOriginMapping>? Mappings { get; set; } = null;

        /// <summary>
        /// True when any configuration collection contains at least one element.
        /// </summary>
        public bool HasAny
        {
            get
            {
                return (Endpoints != null && Endpoints.Any())
                    || (Origins != null && Origins.Any())
                    || (Routes != null && Routes.Any())
                    || (Mappings != null && Mappings.Any());
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ConfigValidationRequest()
        {
        }

        #endregion
    }
}
