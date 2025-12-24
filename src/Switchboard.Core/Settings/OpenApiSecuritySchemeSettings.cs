namespace Switchboard.Core.Settings
{
    /// <summary>
    /// OpenAPI security scheme settings.
    /// </summary>
    public class OpenApiSecuritySchemeSettings
    {
        #region Public-Members

        /// <summary>
        /// Type of security scheme (apiKey, http, oauth2, openIdConnect).
        /// </summary>
        public string Type { get; set; } = "http";

        /// <summary>
        /// Description of the security scheme.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// Name of the header, query, or cookie parameter (for apiKey type).
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Location of the API key (query, header, cookie) for apiKey type.
        /// </summary>
        public string In { get; set; } = null;

        /// <summary>
        /// HTTP authorization scheme (bearer, basic, etc.) for http type.
        /// </summary>
        public string Scheme { get; set; } = "bearer";

        /// <summary>
        /// Bearer format hint for documentation.
        /// </summary>
        public string BearerFormat { get; set; } = "JWT";

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OpenApiSecuritySchemeSettings()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
