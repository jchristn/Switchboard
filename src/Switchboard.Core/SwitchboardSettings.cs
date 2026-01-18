namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Switchboard.Core.Settings;
    using WatsonWebserver.Core;

    /// <summary>
    /// Settings.
    /// </summary>
    public class SwitchboardSettings
    {
        #region Public-Members

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging
        {
            get
            {
                return _Logging;
            }
            set
            {
                if (value == null) value = new LoggingSettings();
                _Logging = value;
            }
        }

        /// <summary>
        /// Endpoints.
        /// </summary>
        public List<ApiEndpoint> Endpoints
        {
            get
            {
                return _Endpoints;
            }
            set
            {
                if (value == null) value = new List<ApiEndpoint>();
                _Endpoints = value;
            }
        }

        /// <summary>
        /// Origin servers.
        /// </summary>
        public List<OriginServer> Origins
        {
            get
            {
                return _Origins;
            }
            set
            {
                if (value == null) value = new List<OriginServer>();
                _Origins = value;
            }
        }

        /// <summary>
        /// List of blocked headers.  These headers are not forwarded from incoming requests to origin servers.
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
        /// Webserver settings.
        /// </summary>
        public WebserverSettings Webserver
        {
            get
            {
                return _Webserver;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Webserver));
                _Webserver = value;
            }
        }

        /// <summary>
        /// OpenAPI documentation settings.
        /// Set Enable to true to generate OpenAPI documentation.
        /// </summary>
        public OpenApiDocumentSettings OpenApi
        {
            get
            {
                return _OpenApi;
            }
            set
            {
                if (value == null) value = new OpenApiDocumentSettings();
                _OpenApi = value;
            }
        }

        /// <summary>
        /// Database configuration settings.
        /// Enable to store configuration in database instead of JSON file.
        /// </summary>
        public DatabaseSettings Database
        {
            get
            {
                return _Database;
            }
            set
            {
                if (value == null) value = new DatabaseSettings();
                _Database = value;
            }
        }

        /// <summary>
        /// Management API settings.
        /// Enable to expose REST API for configuration management.
        /// </summary>
        public ManagementSettings Management
        {
            get
            {
                return _Management;
            }
            set
            {
                if (value == null) value = new ManagementSettings();
                _Management = value;
            }
        }

        /// <summary>
        /// Request history capture settings.
        /// Enable to capture and store request/response history.
        /// </summary>
        public RequestHistorySettings RequestHistory
        {
            get
            {
                return _RequestHistory;
            }
            set
            {
                if (value == null) value = new RequestHistorySettings();
                _RequestHistory = value;
            }
        }

        #endregion

        #region Private-Members

        private LoggingSettings _Logging = new LoggingSettings();
        private List<ApiEndpoint> _Endpoints = new List<ApiEndpoint>();
        private List<OriginServer> _Origins = new List<OriginServer>();
        private WebserverSettings _Webserver = new WebserverSettings();
        private OpenApiDocumentSettings _OpenApi = new OpenApiDocumentSettings();
        private DatabaseSettings _Database = new DatabaseSettings();
        private ManagementSettings _Management = new ManagementSettings();
        private RequestHistorySettings _RequestHistory = new RequestHistorySettings();
        private List<string> _BlockedHeaders = new List<string>
        {
            "alt-svc",
            "connection",
            "date",
            "host",
            "keep-alive",
            "proxy-authorization",
            "proxy-connection",
            "set-cookie",
            "transfer-encoding",
            "upgrade",
            "via",
            "x-forwarded-for",
            "x-request-id"
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SwitchboardSettings()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
