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

        #endregion

        #region Private-Members

        private LoggingSettings _Logging = new LoggingSettings();
        private List<ApiEndpoint> _Endpoints = new List<ApiEndpoint>();
        private List<OriginServer> _Origins = new List<OriginServer>();
        private WebserverSettings _Webserver = new WebserverSettings();
        private List<string> _BlockedHeaders = new List<string>
        {
            "alt-svc",
            "connection",
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
