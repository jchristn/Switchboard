namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Matching API endpoint.
    /// </summary>
    public class MatchingApiEndpoint
    {
        #region Public-Members

        /// <summary>
        /// Boolean indicating if the endpoint requires authentication.
        /// </summary>
        public bool AuthRequired { get; set; } = false;

        /// <summary>
        /// API endpoint.
        /// </summary>
        public ApiEndpoint Endpoint { get; set; } = null;

        /// <summary>
        /// Parameterized URL.
        /// </summary>
        public string ParameterizedUrl { get; set; } = null;

        /// <summary>
        /// URL parameters.
        /// </summary>
        public NameValueCollection Parameters
        {
            get
            {
                return _Parameters;
            }
            set
            {
                if (value == null) value = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                _Parameters = value;
            }
        }

        #endregion

        #region Private-Members

        private NameValueCollection _Parameters = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Matching API endpoint.
        /// </summary>
        public MatchingApiEndpoint()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
