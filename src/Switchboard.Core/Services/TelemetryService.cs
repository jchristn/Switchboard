#nullable enable

namespace Switchboard.Core.Services
{
    using System;

    using OpenTelemetry;
    using OpenTelemetry.Exporter;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;

    using SyslogLogging;

    using Switchboard.Core.Settings;
    using Switchboard.Core.Telemetry;

    /// <summary>
    /// Owns the OpenTelemetry metric and trace providers for the process. Constructed only when
    /// <see cref="TelemetrySettings.Enable"/> is true; wires the shared
    /// <see cref="SwitchboardTelemetry.Meter"/> and <see cref="SwitchboardTelemetry.ActivitySource"/> to an
    /// OTLP exporter so metrics and traces flow to the configured collector.
    /// </summary>
    /// <remarks>
    /// Logs are collected out-of-process by the OpenTelemetry Collector's filelog receiver tailing the
    /// Switchboard log files, so no in-process log provider is created here. Thread safety: this type is
    /// constructed once during daemon startup and disposed once during shutdown.
    /// </remarks>
    public class TelemetryService : IDisposable
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly string _Header = "[TelemetryService] ";
        private readonly LoggingModule _Logging;
        private readonly TelemetrySettings _Settings;

        private MeterProvider? _MeterProvider = null;
        private TracerProvider? _TracerProvider = null;
        private bool _IsDisposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate and build the enabled telemetry providers.
        /// </summary>
        /// <param name="settings">Telemetry settings; may not be null.</param>
        /// <param name="rootSettings">Root settings whose origins/endpoints back the observable gauges; may not be null.</param>
        /// <param name="logging">Logging module; may not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public TelemetryService(TelemetrySettings settings, SwitchboardSettings rootSettings, LoggingModule logging)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(rootSettings);
            ArgumentNullException.ThrowIfNull(logging);

            _Settings = settings;
            _Logging = logging;

            SwitchboardTelemetry.SetSettings(rootSettings);

            ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault().AddService(
                serviceName: _Settings.ServiceName,
                serviceVersion: Constants.SoftwareVersion,
                serviceInstanceId: Guid.NewGuid().ToString());

            if (_Settings.Metrics.Enable)
            {
                _MeterProvider = Sdk.CreateMeterProviderBuilder()
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(SwitchboardTelemetry.SourceName)
                    .AddOtlpExporter((exporterOptions, readerOptions) =>
                    {
                        ConfigureOtlpExporter(exporterOptions);
                        readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = _Settings.Metrics.ExportIntervalMs;
                    })
                    .Build();

                _Logging.Info(_Header + "metrics provider started, exporting via OTLP to " + _Settings.Otlp.Endpoint);
            }

            if (_Settings.Traces.Enable)
            {
                _TracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(SwitchboardTelemetry.SourceName)
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(_Settings.Traces.SamplingRatio)))
                    .AddOtlpExporter(ConfigureOtlpExporter)
                    .Build();

                _Logging.Info(_Header + "tracing provider started, exporting via OTLP to " + _Settings.Otlp.Endpoint);
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_IsDisposed)
            {
                if (disposing)
                {
                    _MeterProvider?.Dispose();
                    _MeterProvider = null;

                    _TracerProvider?.Dispose();
                    _TracerProvider = null;

                    SwitchboardTelemetry.ClearSettings();
                }

                _IsDisposed = true;
            }
        }

        private void ConfigureOtlpExporter(OtlpExporterOptions options)
        {
            options.Endpoint = new Uri(_Settings.Otlp.Endpoint);
            options.Protocol = String.Equals(_Settings.Otlp.Protocol, "httpprotobuf", StringComparison.OrdinalIgnoreCase)
                ? OtlpExportProtocol.HttpProtobuf
                : OtlpExportProtocol.Grpc;
            options.TimeoutMilliseconds = _Settings.Otlp.TimeoutMs;
            if (!String.IsNullOrEmpty(_Settings.Otlp.Headers)) options.Headers = _Settings.Otlp.Headers;
        }

        #endregion
    }
}
