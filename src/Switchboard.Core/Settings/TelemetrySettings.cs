#nullable enable

namespace Switchboard.Core.Settings
{
    using System;

    /// <summary>
    /// Root settings block for OpenTelemetry observability (metrics, traces, and logs).
    /// When <see cref="Enable"/> is false, no telemetry providers are constructed and instrumentation is inert.
    /// </summary>
    public class TelemetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable telemetry. When false, no metric, trace, or log providers are created.
        /// Default is false.
        /// </summary>
        public bool Enable { get; set; } = false;

        /// <summary>
        /// Logical service name reported as the <c>service.name</c> resource attribute.
        /// Default is <c>switchboard</c>.
        /// </summary>
        public string ServiceName
        {
            get => _ServiceName;
            set
            {
                if (String.IsNullOrEmpty(value)) value = "switchboard";
                _ServiceName = value;
            }
        }

        /// <summary>
        /// Metric collection and export settings.
        /// </summary>
        public TelemetryMetricsSettings Metrics
        {
            get => _Metrics;
            set => _Metrics = value ?? new TelemetryMetricsSettings();
        }

        /// <summary>
        /// Distributed tracing settings.
        /// </summary>
        public TelemetryTracesSettings Traces
        {
            get => _Traces;
            set => _Traces = value ?? new TelemetryTracesSettings();
        }

        /// <summary>
        /// Log export settings.
        /// </summary>
        public TelemetryLogsSettings Logs
        {
            get => _Logs;
            set => _Logs = value ?? new TelemetryLogsSettings();
        }

        /// <summary>
        /// OTLP exporter settings shared by all three signals.
        /// </summary>
        public OtlpExporterSettings Otlp
        {
            get => _Otlp;
            set => _Otlp = value ?? new OtlpExporterSettings();
        }

        #endregion

        #region Private-Members

        private string _ServiceName = "switchboard";
        private TelemetryMetricsSettings _Metrics = new TelemetryMetricsSettings();
        private TelemetryTracesSettings _Traces = new TelemetryTracesSettings();
        private TelemetryLogsSettings _Logs = new TelemetryLogsSettings();
        private OtlpExporterSettings _Otlp = new OtlpExporterSettings();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetrySettings()
        {
        }

        #endregion
    }
}
