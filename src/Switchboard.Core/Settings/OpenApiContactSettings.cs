namespace Switchboard.Core.Settings
{
    /// <summary>
    /// OpenAPI contact information settings.
    /// </summary>
    public class OpenApiContactSettings
    {
        #region Public-Members

        /// <summary>
        /// Contact name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Contact URL.
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// Contact email.
        /// </summary>
        public string Email { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OpenApiContactSettings()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
