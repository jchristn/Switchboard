#nullable enable

namespace Switchboard.Core.Models
{
    using System;

    /// <summary>
    /// Schema version tracking record.
    /// Used for database migrations.
    /// </summary>
    public class SchemaVersion
    {
        #region Public-Members

        /// <summary>
        /// Schema version number.
        /// Primary key.
        /// </summary>
        public int Version
        {
            get => _Version;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(Version));
                _Version = value;
            }
        }

        /// <summary>
        /// Timestamp when this version was applied.
        /// </summary>
        public DateTime AppliedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Description of changes in this version.
        /// </summary>
        public string? Description { get; set; } = null;

        #endregion

        #region Private-Members

        private int _Version = 1;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SchemaVersion()
        {
        }

        /// <summary>
        /// Instantiate with version number.
        /// </summary>
        /// <param name="version">Version number.</param>
        public SchemaVersion(int version)
        {
            Version = version;
        }

        /// <summary>
        /// Instantiate with version number and description.
        /// </summary>
        /// <param name="version">Version number.</param>
        /// <param name="description">Description.</param>
        public SchemaVersion(int version, string description)
        {
            Version = version;
            Description = description;
        }

        #endregion
    }
}
