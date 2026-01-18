#nullable enable

namespace Switchboard.Core.Settings
{
    using System;

    /// <summary>
    /// Settings for request history capture.
    /// </summary>
    public class RequestHistorySettings
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable request history capture.
        /// Default is true.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Capture request body.
        /// Default is false.
        /// </summary>
        public bool CaptureRequestBody { get; set; } = false;

        /// <summary>
        /// Capture response body.
        /// Default is false.
        /// </summary>
        public bool CaptureResponseBody { get; set; } = false;

        /// <summary>
        /// Capture request headers.
        /// Default is true.
        /// </summary>
        public bool CaptureRequestHeaders { get; set; } = true;

        /// <summary>
        /// Capture response headers.
        /// Default is true.
        /// </summary>
        public bool CaptureResponseHeaders { get; set; } = true;

        /// <summary>
        /// Maximum request body size to capture in bytes.
        /// Bodies larger than this are not captured.
        /// Default is 65536 (64KB).
        /// </summary>
        public int MaxRequestBodySize
        {
            get => _MaxRequestBodySize;
            set
            {
                if (value < 0) value = 0;
                if (value > 10485760) value = 10485760; // Max 10MB
                _MaxRequestBodySize = value;
            }
        }

        /// <summary>
        /// Maximum response body size to capture in bytes.
        /// Bodies larger than this are not captured.
        /// Default is 65536 (64KB).
        /// </summary>
        public int MaxResponseBodySize
        {
            get => _MaxResponseBodySize;
            set
            {
                if (value < 0) value = 0;
                if (value > 10485760) value = 10485760; // Max 10MB
                _MaxResponseBodySize = value;
            }
        }

        /// <summary>
        /// Retention period in days.
        /// Records older than this are automatically deleted.
        /// Default is 7 days.
        /// Set to 0 for no retention limit.
        /// </summary>
        public int RetentionDays
        {
            get => _RetentionDays;
            set
            {
                if (value < 0) value = 0;
                _RetentionDays = value;
            }
        }

        /// <summary>
        /// Maximum number of records to retain.
        /// When exceeded, oldest records are deleted.
        /// Default is 100000.
        /// Set to 0 for no limit.
        /// </summary>
        public int MaxRecords
        {
            get => _MaxRecords;
            set
            {
                if (value < 0) value = 0;
                _MaxRecords = value;
            }
        }

        /// <summary>
        /// Cleanup interval in seconds.
        /// How often to run the retention cleanup.
        /// Default is 3600 (1 hour).
        /// </summary>
        public int CleanupIntervalSeconds
        {
            get => _CleanupIntervalSeconds;
            set
            {
                if (value < 60) value = 60;
                if (value > 86400) value = 86400; // Max 24 hours
                _CleanupIntervalSeconds = value;
            }
        }

        #endregion

        #region Private-Members

        private int _MaxRequestBodySize = 65536;
        private int _MaxResponseBodySize = 65536;
        private int _RetentionDays = 7;
        private int _MaxRecords = 100000;
        private int _CleanupIntervalSeconds = 3600;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestHistorySettings()
        {
        }

        #endregion
    }
}
