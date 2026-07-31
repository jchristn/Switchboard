#nullable enable

namespace Switchboard.Core.Settings
{
    using System;

    /// <summary>
    /// Settings that control OpenTelemetry distributed tracing.
    /// </summary>
    public class TelemetryTracesSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable trace collection and export.
        /// Default is true.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Ratio of traces to sample, from 0.0 (none) to 1.0 (all).
        /// Default is 1.0.
        /// Minimum is 0.0, maximum is 1.0.
        /// </summary>
        public double SamplingRatio
        {
            get => _SamplingRatio;
            set
            {
                if (value < 0.0) value = 0.0;
                if (value > 1.0) value = 1.0;
                _SamplingRatio = value;
            }
        }

        /// <summary>
        /// When true, Switchboard explicitly injects the active trace context onto the outbound request to
        /// the origin server as W3C <c>traceparent</c>/<c>tracestate</c> headers so the origin can continue
        /// the trace. Note that the .NET runtime also propagates trace context automatically whenever a
        /// trace is active, so context may still reach the origin when a span is being recorded even if this
        /// is false; this flag governs Switchboard's own explicit injection (which also carries tracestate).
        /// Default is true.
        /// </summary>
        public bool PropagateToOrigin { get; set; } = true;

        #endregion

        #region Private-Members

        private double _SamplingRatio = 1.0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetryTracesSettings()
        {
        }

        #endregion
    }
}
