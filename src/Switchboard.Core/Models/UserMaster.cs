#nullable enable

namespace Switchboard.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// User master record.
    /// Represents a user account for management API access.
    /// </summary>
    public class UserMaster
    {
        #region Public-Members

        /// <summary>
        /// Unique identifier for this user.
        /// Primary key.
        /// </summary>
        public Guid GUID
        {
            get => _GUID;
            set
            {
                if (value == Guid.Empty) throw new ArgumentException("GUID cannot be empty.", nameof(GUID));
                _GUID = value;
            }
        }

        /// <summary>
        /// Username for login.
        /// </summary>
        public string Username
        {
            get => _Username;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Username));
                _Username = value;
            }
        }

        /// <summary>
        /// User's email address.
        /// </summary>
        public string? Email { get; set; } = null;

        /// <summary>
        /// User's first name.
        /// </summary>
        public string? FirstName { get; set; } = null;

        /// <summary>
        /// User's last name.
        /// </summary>
        public string? LastName { get; set; } = null;

        /// <summary>
        /// True if the user is active and can log in.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// True if the user has administrator privileges.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Timestamp when this record was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when this record was last modified.
        /// </summary>
        public DateTime? ModifiedUtc { get; set; } = null;

        /// <summary>
        /// Timestamp of the user's last login.
        /// </summary>
        public DateTime? LastLoginUtc { get; set; } = null;

        /// <summary>
        /// Display name for the user.
        /// </summary>
        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                if (!String.IsNullOrEmpty(FirstName) && !String.IsNullOrEmpty(LastName))
                    return FirstName + " " + LastName;
                if (!String.IsNullOrEmpty(FirstName))
                    return FirstName;
                if (!String.IsNullOrEmpty(LastName))
                    return LastName;
                return Username;
            }
        }

        #endregion

        #region Private-Members

        private Guid _GUID = Guid.NewGuid();
        private string _Username = string.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public UserMaster()
        {
        }

        /// <summary>
        /// Instantiate with username.
        /// </summary>
        /// <param name="username">Username.</param>
        public UserMaster(string username)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
        }

        /// <summary>
        /// Instantiate with username and admin status.
        /// </summary>
        /// <param name="username">Username.</param>
        /// <param name="isAdmin">True if administrator.</param>
        public UserMaster(string username, bool isAdmin)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
            IsAdmin = isAdmin;
        }

        #endregion
    }
}
