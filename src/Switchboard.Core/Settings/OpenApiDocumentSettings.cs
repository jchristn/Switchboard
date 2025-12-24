namespace Switchboard.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// OpenAPI document generation settings.
    /// </summary>
    public class OpenApiDocumentSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable OpenAPI document generation.
        /// Default is false.
        /// </summary>
        public bool Enable { get; set; } = false;

        /// <summary>
        /// Enable or disable Swagger UI.
        /// Default is true when OpenAPI is enabled.
        /// </summary>
        public bool EnableSwaggerUi { get; set; } = true;

        /// <summary>
        /// Path to serve the OpenAPI JSON document.
        /// Default is "/openapi.json".
        /// </summary>
        public string DocumentPath
        {
            get
            {
                return _DocumentPath;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) value = "/openapi.json";
                _DocumentPath = value;
            }
        }

        /// <summary>
        /// Path to serve the Swagger UI.
        /// Default is "/swagger".
        /// </summary>
        public string SwaggerUiPath
        {
            get
            {
                return _SwaggerUiPath;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) value = "/swagger";
                _SwaggerUiPath = value;
            }
        }

        /// <summary>
        /// API title for the OpenAPI document.
        /// </summary>
        public string Title { get; set; } = "Switchboard API";

        /// <summary>
        /// API version for the OpenAPI document.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// API description for the OpenAPI document.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// Terms of service URL.
        /// </summary>
        public string TermsOfService { get; set; } = null;

        /// <summary>
        /// Contact information.
        /// </summary>
        public OpenApiContactSettings Contact
        {
            get
            {
                return _Contact;
            }
            set
            {
                if (value == null) value = new OpenApiContactSettings();
                _Contact = value;
            }
        }

        /// <summary>
        /// License information.
        /// </summary>
        public OpenApiLicenseSettings License
        {
            get
            {
                return _License;
            }
            set
            {
                if (value == null) value = new OpenApiLicenseSettings();
                _License = value;
            }
        }

        /// <summary>
        /// Server URLs for the API.
        /// </summary>
        public List<OpenApiServerSettings> Servers
        {
            get
            {
                return _Servers;
            }
            set
            {
                if (value == null) value = new List<OpenApiServerSettings>();
                _Servers = value;
            }
        }

        /// <summary>
        /// Security schemes available for the API.
        /// Key is the scheme name (e.g., "bearerAuth").
        /// </summary>
        public Dictionary<string, OpenApiSecuritySchemeSettings> SecuritySchemes
        {
            get
            {
                return _SecuritySchemes;
            }
            set
            {
                if (value == null) value = new Dictionary<string, OpenApiSecuritySchemeSettings>();
                _SecuritySchemes = value;
            }
        }

        /// <summary>
        /// Global tags for organizing operations.
        /// </summary>
        public List<OpenApiTagSettings> Tags
        {
            get
            {
                return _Tags;
            }
            set
            {
                if (value == null) value = new List<OpenApiTagSettings>();
                _Tags = value;
            }
        }

        #endregion

        #region Private-Members

        private string _DocumentPath = "/openapi.json";
        private string _SwaggerUiPath = "/swagger";
        private OpenApiContactSettings _Contact = new OpenApiContactSettings();
        private OpenApiLicenseSettings _License = new OpenApiLicenseSettings();
        private List<OpenApiServerSettings> _Servers = new List<OpenApiServerSettings>();
        private Dictionary<string, OpenApiSecuritySchemeSettings> _SecuritySchemes = new Dictionary<string, OpenApiSecuritySchemeSettings>();
        private List<OpenApiTagSettings> _Tags = new List<OpenApiTagSettings>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OpenApiDocumentSettings()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
