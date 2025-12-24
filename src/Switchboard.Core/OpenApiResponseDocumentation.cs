namespace Switchboard.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// OpenAPI response documentation.
    /// </summary>
    public class OpenApiResponseDocumentation
    {
        #region Public-Members

        /// <summary>
        /// Description of the response.
        /// </summary>
        public string Description { get; set; } = null;

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
        public OpenApiResponseDocumentation()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
