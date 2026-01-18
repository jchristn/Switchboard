#nullable enable

namespace Switchboard.Core.Models
{
    using System;

    /// <summary>
    /// Globally blocked header.
    /// Headers in this table are not forwarded from incoming requests to origin servers.
    /// </summary>
    public class BlockedHeader
    {
        #region Public-Members

        /// <summary>
        /// Auto-incremented primary key.
        /// </summary>
        public int Id { get; set; } = 0;

        /// <summary>
        /// Header name (case-insensitive).
        /// </summary>
        public string HeaderName
        {
            get => _HeaderName;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(HeaderName));
                _HeaderName = value.ToLowerInvariant();
            }
        }

        /// <summary>
        /// Timestamp when this record was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private string _HeaderName = string.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BlockedHeader()
        {
        }

        /// <summary>
        /// Instantiate with header name.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        public BlockedHeader(string headerName)
        {
            HeaderName = headerName ?? throw new ArgumentNullException(nameof(headerName));
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
