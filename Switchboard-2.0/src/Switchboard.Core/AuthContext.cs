namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using SerializationHelper;

    /// <summary>
    /// Auth context.
    /// </summary>
    public class AuthContext
    {
        #region Public-Members

        /// <summary>
        /// Authentication context.
        /// </summary>
        public AuthenticationContext Authentication
        {
            get
            {
                return _Authentication;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Authentication));
                _Authentication = value;
            }
        }

        /// <summary>
        /// Authorization context.
        /// </summary>
        public AuthorizationContext Authorization
        {
            get
            {
                return _Authorization;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Authorization));
                _Authorization = value;
            }
        }

        /// <summary>
        /// User-assignable metadata, useful for passing context to and from your application and Switchboard.
        /// This object must be serializable to JSON.
        /// </summary>
        public object Metadata { get; set; } = null;

        /// <summary>
        /// Message to provide to the user in the event that authentication or authorization was not successful.
        /// </summary>
        public string FailureMessage { get; set; } = null;

        #endregion

        #region Private-Members

        private AuthenticationContext _Authentication = new AuthenticationContext();
        private AuthorizationContext _Authorization = new AuthorizationContext();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Auth context.
        /// </summary>
        public AuthContext()
        {

        }

        /// <summary>
        /// Return a populated authentication result object from a supplied base64 string.
        /// </summary>
        /// <param name="base64">Base64 string.</param>
        /// <param name="serializer">Serializer.</param>
        /// <returns>AuthenticationResult.</returns>
        public static AuthContext FromBase64String(string base64, Serializer serializer)
        {
            if (String.IsNullOrEmpty(base64)) throw new ArgumentNullException(nameof(base64));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            byte[] bytes = Convert.FromBase64String(base64);
            string json = Encoding.UTF8.GetString(bytes);
            return serializer.DeserializeJson<AuthContext>(json);
        }

        /// <summary>
        /// Try to parse a base64 string into an authentication context.
        /// </summary>
        /// <param name="base64">Base64 string.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="authContext">Auth context.</param>
        /// <returns></returns>
        public static bool TryFromBase64String(string base64, Serializer serializer, out AuthContext authContext)
        {
            if (String.IsNullOrEmpty(base64)) throw new ArgumentNullException(nameof(base64));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            authContext = null;

            try
            {
                authContext = AuthContext.FromBase64String(base64, serializer);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a base64 representation of the authentication context.
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

        #endregion

        #region Private-Methods

        #endregion
    }
}
