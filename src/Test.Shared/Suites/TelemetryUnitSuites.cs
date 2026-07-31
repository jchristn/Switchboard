namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading.Tasks;

    using Switchboard.Core;
    using Switchboard.Core.Settings;
    using Switchboard.Core.Telemetry;
    using Touchstone.Core;

    /// <summary>
    /// Network-free unit tests for telemetry settings (defaults and value clamping) and the instrument
    /// registry (<see cref="SwitchboardTelemetry"/>). Metric recording is validated with an in-process
    /// <see cref="MeterListener"/> so the metric names and labels are pinned against the documented
    /// catalog without binding any ports. Positive and negative assertions are included per behavior.
    /// </summary>
    public static class TelemetryUnitSuites
    {
        /// <summary>
        /// All telemetry unit suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the telemetry unit suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "TelemetryUnit",
                displayName: "Telemetry (unit)",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Defaults", "Telemetry settings expose the documented defaults", () =>
                    {
                        TelemetrySettings settings = new TelemetrySettings();
                        Check.False(settings.Enable, "telemetry disabled by default");
                        Check.Equal("switchboard", settings.ServiceName, "default service name");
                        Check.True(settings.Metrics.Enable, "metrics enabled by default");
                        Check.Equal(60000, settings.Metrics.ExportIntervalMs, "default metric export interval");
                        Check.True(settings.Traces.Enable, "traces enabled by default");
                        Check.Equal(1.0, settings.Traces.SamplingRatio, "default sampling ratio");
                        Check.True(settings.Traces.PropagateToOrigin, "propagation on by default");
                        Check.True(settings.Logs.Enable, "logs enabled by default");
                        Check.Equal(1, settings.Logs.MinimumSeverity, "default log severity floor");
                        Check.Equal("http://localhost:4317", settings.Otlp.Endpoint, "default OTLP endpoint");
                        Check.Equal("grpc", settings.Otlp.Protocol, "default OTLP protocol");
                        Check.Equal(10000, settings.Otlp.TimeoutMs, "default OTLP timeout");
                        Check.True(settings.Otlp.Headers == null, "no OTLP headers by default");
                    }),

                    Case("ExportIntervalClamp", "Metric export interval clamps to [1000, 300000]", () =>
                    {
                        TelemetryMetricsSettings m = new TelemetryMetricsSettings();
                        m.ExportIntervalMs = 10;
                        Check.Equal(1000, m.ExportIntervalMs, "below-minimum clamps up to 1000");
                        m.ExportIntervalMs = 999999;
                        Check.Equal(300000, m.ExportIntervalMs, "above-maximum clamps down to 300000");
                        m.ExportIntervalMs = 15000;
                        Check.Equal(15000, m.ExportIntervalMs, "in-range value preserved");
                    }),

                    Case("SamplingRatioClamp", "Trace sampling ratio clamps to [0.0, 1.0]", () =>
                    {
                        TelemetryTracesSettings t = new TelemetryTracesSettings();
                        t.SamplingRatio = -0.5;
                        Check.Equal(0.0, t.SamplingRatio, "negative clamps to 0.0");
                        t.SamplingRatio = 2.0;
                        Check.Equal(1.0, t.SamplingRatio, "above 1 clamps to 1.0");
                        t.SamplingRatio = 0.25;
                        Check.Equal(0.25, t.SamplingRatio, "in-range value preserved");
                    }),

                    Case("LogSeverityClamp", "Log minimum severity clamps to [0, 7]", () =>
                    {
                        TelemetryLogsSettings l = new TelemetryLogsSettings();
                        l.MinimumSeverity = -3;
                        Check.Equal(0, l.MinimumSeverity, "negative clamps to 0");
                        l.MinimumSeverity = 42;
                        Check.Equal(7, l.MinimumSeverity, "above 7 clamps to 7");
                    }),

                    Case("OtlpTimeoutClamp", "OTLP timeout clamps to [1000, 120000]", () =>
                    {
                        OtlpExporterSettings o = new OtlpExporterSettings();
                        o.TimeoutMs = 5;
                        Check.Equal(1000, o.TimeoutMs, "below-minimum clamps up");
                        o.TimeoutMs = 999999;
                        Check.Equal(120000, o.TimeoutMs, "above-maximum clamps down");
                    }),

                    Case("OtlpProtocolNormalization", "OTLP protocol normalizes to grpc/httpprotobuf", () =>
                    {
                        OtlpExporterSettings o = new OtlpExporterSettings();
                        o.Protocol = "HttpProtobuf";
                        Check.Equal("httpprotobuf", o.Protocol, "case-insensitive httpprotobuf accepted");
                        o.Protocol = "GRPC";
                        Check.Equal("grpc", o.Protocol, "case-insensitive grpc accepted");
                        o.Protocol = "nonsense";
                        Check.Equal("grpc", o.Protocol, "unrecognized value falls back to grpc");
                    }),

                    Case("StringDefaultsGuarded", "Empty service name and endpoint fall back to defaults", () =>
                    {
                        TelemetrySettings settings = new TelemetrySettings();
                        settings.ServiceName = "";
                        Check.Equal("switchboard", settings.ServiceName, "empty service name restored");
                        OtlpExporterSettings o = new OtlpExporterSettings();
                        o.Endpoint = "";
                        Check.Equal("http://localhost:4317", o.Endpoint, "empty endpoint restored");
                    }),

                    Case("NullSubObjectsReplaced", "Null sub-settings are replaced with defaults, not stored", () =>
                    {
                        TelemetrySettings settings = new TelemetrySettings();
                        settings.Metrics = null!;
                        settings.Traces = null!;
                        settings.Logs = null!;
                        settings.Otlp = null!;
                        Check.True(settings.Metrics != null, "metrics never null");
                        Check.True(settings.Traces != null, "traces never null");
                        Check.True(settings.Logs != null, "logs never null");
                        Check.True(settings.Otlp != null, "otlp never null");
                    }),

                    Case("MeterAndSourceNames", "Meter and activity source are named Switchboard", () =>
                    {
                        Check.Equal("Switchboard", SwitchboardTelemetry.SourceName, "shared source name");
                        Check.Equal("Switchboard", SwitchboardTelemetry.Meter.Name, "meter name");
                        Check.Equal("Switchboard", SwitchboardTelemetry.ActivitySource.Name, "activity source name");
                    }),

                    Case("CounterCatalog", "Recording methods emit the documented metric names and labels", () =>
                    {
                        List<Sample> samples = Capture(false, () =>
                        {
                            SwitchboardTelemetry.RecordRequest("ep1", "GET", 200);
                            SwitchboardTelemetry.RecordRejection(429);
                            SwitchboardTelemetry.RecordOriginRequest("origin1", 503);
                            SwitchboardTelemetry.RecordDuration("ep1", "origin1", 0.125);
                            SwitchboardTelemetry.RecordBodySizes("ep1", 1024, 2048);
                            SwitchboardTelemetry.RecordHealthCheck("origin1", true);
                            SwitchboardTelemetry.RecordEjection("origin1");
                            SwitchboardTelemetry.RecordSelection("ep1", "origin1");
                            SwitchboardTelemetry.RecordRetry("ep1");
                            SwitchboardTelemetry.RecordFailover("ep1");
                        });

                        Sample req = Single(samples, "switchboard_requests_total");
                        Check.Equal("ep1", req.Tag("endpoint"), "request endpoint label");
                        Check.Equal("GET", req.Tag("method"), "request method label");
                        Check.Equal("200", req.Tag("code"), "request code label");

                        Check.Equal("429", Single(samples, "switchboard_gateway_rejections_total").Tag("reason"), "rejection reason label");
                        Check.Equal("503", Single(samples, "switchboard_origin_requests_total").Tag("code"), "origin request code label");
                        Check.Equal(0.125, Single(samples, "switchboard_request_duration_seconds").Value, "duration value in seconds");
                        Check.Equal(1024.0, Single(samples, "switchboard_request_body_bytes").Value, "request body bytes");
                        Check.Equal(2048.0, Single(samples, "switchboard_response_body_bytes").Value, "response body bytes");
                        Check.Equal("success", Single(samples, "switchboard_origin_health_checks_total").Tag("result"), "health check result label");
                        Check.True(Has(samples, "switchboard_origin_ejections_total"), "ejection counter present");
                        Check.True(Has(samples, "switchboard_lb_selections_total"), "selection counter present");
                        Check.True(Has(samples, "switchboard_retries_total"), "retry counter present");
                        Check.True(Has(samples, "switchboard_failovers_total"), "failover counter present");
                    }),

                    Case("ObservableGaugesSnapshotSettings", "Observable gauges read the registered settings snapshot", () =>
                    {
                        SwitchboardSettings root = new SwitchboardSettings();
                        root.Origins.Add(new OriginServer { Identifier = "o-a", Name = "o-a", Hostname = "127.0.0.1", Port = 9, Healthy = true });
                        root.Origins.Add(new OriginServer { Identifier = "o-b", Name = "o-b", Hostname = "127.0.0.1", Port = 10, Healthy = false });
                        root.Endpoints.Add(new ApiEndpoint { Identifier = "e-a", Name = "e-a" });

                        try
                        {
                            SwitchboardTelemetry.SetSettings(root);
                            List<Sample> samples = Capture(true, null);

                            Check.Equal(2.0, Single(samples, "switchboard_config_origins").Value, "config origins count");
                            Check.Equal(1.0, Single(samples, "switchboard_config_endpoints").Value, "config endpoints count");
                            Check.Equal(1.0, Single(samples, "switchboard_build_info").Value, "build info is 1");
                            Check.Equal(Constants.SoftwareVersion, Single(samples, "switchboard_build_info").Tag("version"), "build info carries version label");

                            List<Sample> up = Matching(samples, "switchboard_origin_up");
                            Check.Equal(2, up.Count, "one up-gauge per origin");
                            Check.Equal(1.0, up.Single(s => s.Tag("origin") == "o-a").Value, "healthy origin reports up=1");
                            Check.Equal(0.0, up.Single(s => s.Tag("origin") == "o-b").Value, "unhealthy origin reports up=0");
                        }
                        finally
                        {
                            SwitchboardTelemetry.ClearSettings();
                        }
                    }),

                    Case("ObservableGaugesInertWithoutSettings", "With no settings registered, per-origin gauges emit nothing", () =>
                    {
                        SwitchboardTelemetry.ClearSettings();
                        List<Sample> samples = Capture(true, null);
                        Check.False(Has(samples, "switchboard_origin_up"), "no origin gauges without settings");
                        Check.False(Has(samples, "switchboard_config_origins"), "no config gauge without settings");
                        // build_info is static and does not depend on settings.
                        Check.True(Has(samples, "switchboard_build_info"), "build info still reported");
                    }),

                    Case("RecordingInertWithoutListener", "Recording with no listener attached is a safe no-op", () =>
                    {
                        // No MeterListener is active here; the calls must not throw.
                        SwitchboardTelemetry.RecordRequest("ep", "GET", 200);
                        SwitchboardTelemetry.RecordDuration("ep", "o", 0.1);
                        SwitchboardTelemetry.RecordSelection("ep", "o");
                        Check.True(true, "recording without a listener did not throw");
                    })
                });
        }

        #region Private-Helpers

        private sealed class Sample
        {
            public string Name = "";
            public double Value;
            public Dictionary<string, string> Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public string? Tag(string key)
            {
                return Tags.TryGetValue(key, out string? value) ? value : null;
            }
        }

        // Attach a MeterListener to the Switchboard meter, run the recording action (if any), optionally
        // pull observable instruments, and return everything captured.
        private static List<Sample> Capture(bool pullObservable, Action? record)
        {
            List<Sample> samples = new List<Sample>();
            object gate = new object();

            using (MeterListener listener = new MeterListener())
            {
                listener.InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == SwitchboardTelemetry.SourceName) l.EnableMeasurementEvents(instrument);
                };
                listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
                {
                    lock (gate) samples.Add(ToSample(instrument.Name, measurement, tags));
                });
                listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
                {
                    lock (gate) samples.Add(ToSample(instrument.Name, measurement, tags));
                });
                listener.Start();

                record?.Invoke();
                if (pullObservable) listener.RecordObservableInstruments();
            }

            return samples;
        }

        private static Sample ToSample(string name, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            Sample sample = new Sample { Name = name, Value = value };
            foreach (KeyValuePair<string, object?> tag in tags)
                sample.Tags[tag.Key] = tag.Value?.ToString() ?? "";
            return sample;
        }

        private static bool Has(List<Sample> samples, string name)
        {
            return samples.Any(s => s.Name == name);
        }

        private static Sample Single(List<Sample> samples, string name)
        {
            List<Sample> matches = Matching(samples, name);
            Check.Equal(1, matches.Count, "exactly one '" + name + "' measurement");
            return matches[0];
        }

        private static List<Sample> Matching(List<Sample> samples, string name)
        {
            return samples.Where(s => s.Name == name).ToList();
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Action body)
        {
            return new TestCaseDescriptor(
                suiteId: "TelemetryUnit",
                caseId: caseId,
                displayName: displayName,
                executeAsync: _ =>
                {
                    body();
                    return Task.CompletedTask;
                });
        }

        #endregion
    }
}
