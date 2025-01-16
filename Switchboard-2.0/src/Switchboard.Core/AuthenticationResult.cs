using SerializationHelper;
using System;
using System.Text;

namespace Switchboard.Core
{
    /// <summary>
    /// Authentication result.
    /// </summary>
    public class AuthenticationResult
    {
        #region Public-Members

        /// <summary>
        /// Authentication result.
        /// </summary>
        public AuthenticationResultEnum Result { get; set; } = AuthenticationResultEnum.Success;

        /// <summary>
        /// Message to provide to the user in the event that authentication was not successful.
        /// </summary>
        public string FailureMessage { get; set; } = null;

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
        /// Authentication result.
        /// </summary>
        public AuthenticationResult()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a base64 representation of the authentication result.
        /// </summary>
        /// <returns>Base64 string.</returns>
        public string ToBase64String(Serializer serializer)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            string json = serializer.SerializeJson(this, false);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            string base64 = Convert.ToBase64String(bytes);
            return base64;
        }

        /// <summary>
        /// Return a populated authentication result object from a supplied base64 string.
        /// </summary>
        /// <param name="base64">Base64 string.</param>
        /// <param name="serializer">Serializer.</param>
        /// <returns>AuthenticationResult.</returns>
        public static AuthenticationResult FromBase64String(string base64, Serializer serializer)
        {
            if (String.IsNullOrEmpty(base64)) throw new ArgumentNullException(nameof(base64));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            byte[] bytes = Convert.FromBase64String(base64);
            string json = Encoding.UTF8.GetString(bytes);
            return serializer.DeserializeJson<AuthenticationResult>(json);
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
