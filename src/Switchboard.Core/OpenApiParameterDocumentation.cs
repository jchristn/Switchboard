namespace Switchboard.Core
{
    /// <summary>
    /// OpenAPI parameter documentation.
    /// </summary>
    public class OpenApiParameterDocumentation
    {
        #region Public-Members

        /// <summary>
        /// Parameter name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Parameter location (path, query, header, cookie).
        /// </summary>
        public string In { get; set; } = "query";

        /// <summary>
        /// Parameter description.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// Whether the parameter is required.
        /// </summary>
        public bool Required { get; set; } = false;

        /// <summary>
        /// Whether the parameter is deprecated.
        /// </summary>
        public bool Deprecated { get; set; } = false;

        /// <summary>
        /// Schema type (string, integer, boolean, array, object).
        /// </summary>
        public string SchemaType { get; set; } = "string";

        /// <summary>
        /// Schema format (e.g., int32, int64, date-time, email).
        /// </summary>
        public string SchemaFormat { get; set; } = null;

        /// <summary>
        /// Example value.
        /// </summary>
        public object Example { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OpenApiParameterDocumentation()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
