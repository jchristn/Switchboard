using SerializationHelper;
using System;
using System.Text;

namespace Switchboard.Core
{
    /// <summary>
    /// Authentication context.
    /// </summary>
    public class AuthenticationContext
    {
        #region Public-Members

        /// <summary>
        /// Authentication result.
        /// </summary>
        public AuthenticationResultEnum Result { get; set; } = AuthenticationResultEnum.Success;

        /// <summary>
        /// User-assignable metadata, useful for passing context to and from your application and Switchboard.
        /// This object must be serializable to JSON.
        /// </summary>
        public object Metadata { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Authentication context.
        /// </summary>
        public AuthenticationContext()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
