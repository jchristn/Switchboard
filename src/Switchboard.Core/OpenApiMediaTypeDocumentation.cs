namespace Switchboard.Core
{
    /// <summary>
    /// OpenAPI media type documentation.
    /// </summary>
    public class OpenApiMediaTypeDocumentation
    {
        #region Public-Members

        /// <summary>
        /// Schema type (string, integer, boolean, array, object).
        /// </summary>
        public string SchemaType { get; set; } = "object";

        /// <summary>
        /// Schema format (e.g., int32, int64, date-time, email, uuid).
        /// </summary>
        public string SchemaFormat { get; set; } = null;

        /// <summary>
        /// Example value (as JSON string for objects).
        /// </summary>
        public object Example { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OpenApiMediaTypeDocumentation()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
