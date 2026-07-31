#nullable enable

namespace Switchboard.Core.Settings
{
    using System;

    /// <summary>
    /// Settings that control OpenTelemetry metric collection and export.
    /// Metrics are exported over OTLP to the configured collector; the collector is responsible for
    /// exposing the Prometheus scrape surface.
    /// </summary>
    public class TelemetryMetricsSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable metric collection and export.
        /// Default is true.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Interval, in milliseconds, at which collected metrics are exported over OTLP.
        /// Default is 60000 (60 seconds).
        /// Minimum is 1000 (1 second), maximum is 300000 (5 minutes).
        /// </summary>
        public int ExportIntervalMs
        {
            get => _ExportIntervalMs;
            set
            {
                if (value < 1000) value = 1000;
                if (value > 300000) value = 300000;
                _ExportIntervalMs = value;
            }
        }

        #endregion

        #region Private-Members

        private int _ExportIntervalMs = 60000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetryMetricsSettings()
        {
        }

        #endregion
    }
}
