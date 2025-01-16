namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;

    /// <summary>
    /// Origin server.
    /// </summary>
    public class OriginServer
    {
        #region Public-Members

        /// <summary>
        /// Unique identifier for this origin server.
        /// </summary>
        public string Identifier { get; set; } = null;

        /// <summary>
        /// Name for this origin server.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Hostname.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// TCP port.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 0 || value > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
                _Port = value;
            }
        }

        /// <summary>
        /// Enable or disable SSL.
        /// </summary>
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// True to log the request body.
        /// </summary>
        public bool LogRequestBody { get; set; } = false;

        /// <summary>
        /// True to log the response body.
        /// </summary>
        public bool LogResponseBody { get; set; } = false;

        /// <summary>
        /// URL.
        /// </summary>
        public string UrlPrefix
        {
            get
            {
                return (Ssl ? "https://" : "http://") + Hostname + ":" + Port;
            }
        }

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 8000;

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
