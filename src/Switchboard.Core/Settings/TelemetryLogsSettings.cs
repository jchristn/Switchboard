#nullable enable

namespace Switchboard.Core.Settings
{
    using System;

    /// <summary>
    /// Settings that control OpenTelemetry log export.
    /// When an app-side log bridge is available, Switchboard log events at or above
    /// <see cref="MinimumSeverity"/> are exported over OTLP; otherwise logs are collected from the log
    /// files by the OpenTelemetry Collector's filelog receiver.
    /// </summary>
    public class TelemetryLogsSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable log export.
        /// Default is true.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Minimum severity to export, mirroring the syslog severity scale
        /// (0=Emergency ... 7=Debug in syslog terms; Switchboard uses 0=Debug..4=Alert for its own floor).
        /// Default is 1.
        /// Minimum is 0, maximum is 7.
        /// </summary>
        public int MinimumSeverity
        {
            get => _MinimumSeverity;
            set
            {
                if (value < 0) value = 0;
                if (value > 7) value = 7;
                _MinimumSeverity = value;
            }
        }

        #endregion

        #region Private-Members

        private int _MinimumSeverity = 1;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetryLogsSettings()
        {
        }

        #endregion
    }
}
