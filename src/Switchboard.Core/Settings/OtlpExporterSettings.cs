#nullable enable

namespace Switchboard.Core.Settings
{
    using System;

    /// <summary>
    /// Settings for the OpenTelemetry OTLP exporter shared by metrics, traces, and logs.
    /// </summary>
    public class OtlpExporterSettings
    {
        #region Public-Members

        /// <summary>
        /// OTLP endpoint URL of the collector or backend that receives telemetry.
        /// Default is <c>http://localhost:4317</c>.
        /// </summary>
        public string Endpoint
        {
            get => _Endpoint;
            set
            {
                if (String.IsNullOrEmpty(value)) value = "http://localhost:4317";
                _Endpoint = value;
            }
        }

        /// <summary>
        /// OTLP wire protocol, either <c>grpc</c> or <c>httpprotobuf</c>.
        /// Default is <c>grpc</c>.
        /// Any unrecognized value falls back to <c>grpc</c>.
        /// </summary>
        public string Protocol
        {
            get => _Protocol;
            set
            {
                if (String.IsNullOrEmpty(value)) value = "grpc";
                string normalized = value.Trim().ToLowerInvariant();
                if (normalized != "grpc" && normalized != "httpprotobuf") normalized = "grpc";
                _Protocol = normalized;
            }
        }

        /// <summary>
        /// Export timeout in milliseconds.
        /// Default is 10000 (10 seconds).
        /// Minimum is 1000 (1 second), maximum is 120000 (2 minutes).
        /// </summary>
        public int TimeoutMs
        {
            get => _TimeoutMs;
            set
            {
                if (value < 1000) value = 1000;
                if (value > 120000) value = 120000;
                _TimeoutMs = value;
            }
        }

        /// <summary>
        /// Optional comma-separated list of <c>key=value</c> headers sent with each OTLP export request,
        /// typically used to authenticate to a hosted telemetry backend.
        /// May carry a secret; treated as sensitive by the management API.
        /// Default is null.
        /// </summary>
        public string? Headers { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Endpoint = "http://localhost:4317";
        private string _Protocol = "grpc";
        private int _TimeoutMs = 10000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OtlpExporterSettings()
        {
        }

        #endregion
    }
}
