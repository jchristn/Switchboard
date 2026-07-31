#nullable enable

namespace Switchboard.Core.Models
{
    using System;

    /// <summary>
    /// A single origin server health check result, recorded in the rolling in-memory history window.
    /// </summary>
    public class HealthCheckRecord
    {
        #region Public-Members

        /// <summary>
        /// Timestamp, in UTC, at which the health check was performed.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// True if the health check succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public HealthCheckRecord()
        {
        }

        /// <summary>
        /// Instantiate with values.
        /// </summary>
        /// <param name="timestampUtc">Timestamp, in UTC, at which the health check was performed.</param>
        /// <param name="success">True if the health check succeeded.</param>
        public HealthCheckRecord(DateTime timestampUtc, bool success)
        {
            TimestampUtc = timestampUtc;
            Success = success;
        }

        #endregion
    }
}
