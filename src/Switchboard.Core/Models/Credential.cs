#nullable enable

namespace Switchboard.Core.Models
{
    using System;
    using System.Security.Cryptography;
    using System.Text.Json.Serialization;

    /// <summary>
    /// User credential (bearer token).
    /// </summary>
    public class Credential
    {
        #region Public-Members

        /// <summary>
        /// Unique identifier for this credential.
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
        /// User GUID (foreign key to UserMaster).
        /// </summary>
        public Guid UserGUID
        {
            get => _UserGUID;
            set
            {
                if (value == Guid.Empty) throw new ArgumentException("UserGUID cannot be empty.", nameof(UserGUID));
                _UserGUID = value;
            }
        }

        /// <summary>
        /// Display name for this credential (e.g., "API Token 1", "CLI Access").
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Description of what this token is used for.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Bearer token value.
        /// This should only be shown once when created.
        /// </summary>
        public string BearerToken
        {
            get => _BearerToken;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(BearerToken));
                _BearerToken = value;
            }
        }

        /// <summary>
        /// SHA256 hash of the bearer token.
        /// Used for lookup and validation without storing the plain token.
        /// </summary>
        [JsonIgnore]
        public string BearerTokenHash
        {
            get => _BearerTokenHash ?? ComputeTokenHash(_BearerToken);
            set => _BearerTokenHash = value;
        }

        /// <summary>
        /// True if this credential is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// True if this credential is read-only and cannot be modified via API or dashboard.
        /// </summary>
        public bool IsReadOnly { get; set; } = false;

        /// <summary>
        /// Timestamp when this credential was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when this credential expires.
        /// Null means no expiration.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Timestamp when this credential was last used.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; } = null;

        /// <summary>
        /// Timestamp when this credential was last modified.
        /// </summary>
        public DateTime? ModifiedUtc { get; set; } = null;

        /// <summary>
        /// True if this credential has expired.
        /// </summary>
        [JsonIgnore]
        public bool IsExpired => ExpiresUtc.HasValue && ExpiresUtc.Value < DateTime.UtcNow;

        /// <summary>
        /// True if this credential is valid (active and not expired).
        /// </summary>
        [JsonIgnore]
        public bool IsValid => Active && !IsExpired;

        #endregion

        #region Private-Members

        private Guid _GUID = Guid.NewGuid();
        private Guid _UserGUID = Guid.Empty;
        private string _BearerToken = string.Empty;
        private string? _BearerTokenHash = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Credential()
        {
        }

        /// <summary>
        /// Instantiate with user GUID.
        /// Generates a new random bearer token.
        /// </summary>
        /// <param name="userGuid">User GUID.</param>
        public Credential(Guid userGuid)
        {
            UserGUID = userGuid;
            BearerToken = GenerateBearerToken();
        }

        /// <summary>
        /// Instantiate with user GUID and custom token.
        /// </summary>
        /// <param name="userGuid">User GUID.</param>
        /// <param name="bearerToken">Bearer token.</param>
        public Credential(Guid userGuid, string bearerToken)
        {
            UserGUID = userGuid;
            BearerToken = bearerToken ?? throw new ArgumentNullException(nameof(bearerToken));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate a new random bearer token.
        /// </summary>
        /// <returns>Random bearer token string.</returns>
        public static string GenerateBearerToken()
        {
            byte[] bytes = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        /// <summary>
        /// Compute SHA256 hash of a bearer token.
        /// </summary>
        /// <param name="token">Bearer token.</param>
        /// <returns>SHA256 hash as base64 string.</returns>
        public static string ComputeTokenHash(string token)
        {
            if (String.IsNullOrEmpty(token)) return string.Empty;
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(token);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToBase64String(hash);
        }

        #endregion
    }
}
