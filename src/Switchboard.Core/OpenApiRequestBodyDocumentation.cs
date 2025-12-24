namespace Switchboard.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// OpenAPI request body documentation.
    /// </summary>
    public class OpenApiRequestBodyDocumentation
    {
        #region Public-Members

        /// <summary>
        /// Description of the request body.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// Whether the request body is required.
        /// </summary>
        public bool Required { get; set; } = false;

        /// <summary>
        /// Content types and their schemas.
        /// Key: Media type (e.g., "application/json").
        /// </summary>
        public Dictionary<string, OpenApiMediaTypeDocumentation> Content
        {
            get
            {
                return _Content;
            }
            set
            {
                if (value == null) value = new Dictionary<string, OpenApiMediaTypeDocumentation>();
                _Content = value;
            }
        }

        #endregion

        #region Private-Members

        private Dictionary<string, OpenApiMediaTypeDocumentation> _Content = new Dictionary<string, OpenApiMediaTypeDocumentation>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OpenApiRequestBodyDocumentation()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
