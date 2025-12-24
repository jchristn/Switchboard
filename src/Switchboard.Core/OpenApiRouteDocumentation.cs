namespace Switchboard.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// OpenAPI documentation for a single route.
    /// </summary>
    public class OpenApiRouteDocumentation
    {
        #region Public-Members

        /// <summary>
        /// Unique operation identifier.
        /// </summary>
        public string OperationId { get; set; } = null;

        /// <summary>
        /// Short summary of the operation.
        /// </summary>
        public string Summary { get; set; } = null;

        /// <summary>
        /// Detailed description of the operation.
        /// CommonMark syntax may be used.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// Tags for grouping the operation.
        /// </summary>
        public List<string> Tags
        {
            get
            {
                return _Tags;
            }
            set
            {
                if (value == null) value = new List<string>();
                _Tags = value;
            }
        }

        /// <summary>
        /// Whether the operation is deprecated.
        /// </summary>
        public bool Deprecated { get; set; } = false;

        /// <summary>
        /// Parameter documentation.
        /// </summary>
        public List<OpenApiParameterDocumentation> Parameters
        {
            get
            {
                return _Parameters;
            }
            set
            {
                if (value == null) value = new List<OpenApiParameterDocumentation>();
                _Parameters = value;
            }
        }

        /// <summary>
        /// Request body documentation.
        /// </summary>
        public OpenApiRequestBodyDocumentation RequestBody { get; set; } = null;

        /// <summary>
        /// Response documentation by status code.
        /// Key: HTTP status code (e.g., "200", "404", "default").
        /// </summary>
        public Dictionary<string, OpenApiResponseDocumentation> Responses
        {
            get
            {
                return _Responses;
            }
            set
            {
                if (value == null) value = new Dictionary<string, OpenApiResponseDocumentation>();
                _Responses = value;
            }
        }

        /// <summary>
        /// Security requirements for this operation.
        /// List of security scheme names.
        /// </summary>
        public List<string> Security
        {
            get
            {
                return _Security;
            }
            set
            {
                if (value == null) value = new List<string>();
                _Security = value;
            }
        }

        #endregion

        #region Private-Members

        private List<string> _Tags = new List<string>();
        private List<OpenApiParameterDocumentation> _Parameters = new List<OpenApiParameterDocumentation>();
        private Dictionary<string, OpenApiResponseDocumentation> _Responses = new Dictionary<string, OpenApiResponseDocumentation>();
        private List<string> _Security = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OpenApiRouteDocumentation()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
