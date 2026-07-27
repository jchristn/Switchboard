#nullable enable

namespace Switchboard.Core.Models
{
    using System;

    /// <summary>
    /// A single fixed-width time bucket of aggregated request history statistics.
    /// Used by the management timeseries endpoint. Empty buckets are zero-filled.
    /// </summary>
    public class TimeSeriesBucket
    {
        #region Public-Members

        /// <summary>
        /// Inclusive start of the bucket window (UTC).
        /// </summary>
        public DateTime BucketStartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total number of requests that fall in this bucket.
        /// </summary>
        public long Total { get; set; } = 0;

        /// <summary>
        /// Number of successful requests in this bucket.
        /// </summary>
        public long Success { get; set; } = 0;

        /// <summary>
        /// Number of failed requests in this bucket.
        /// </summary>
        public long Failure { get; set; } = 0;

        /// <summary>
        /// Average request duration in milliseconds for requests in this bucket.
        /// Zero when the bucket is empty.
        /// </summary>
        public double AvgDurationMs { get; set; } = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TimeSeriesBucket()
        {
        }

        #endregion
    }
}
