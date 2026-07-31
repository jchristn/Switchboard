namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using RestWrapper;
    using Switchboard.Core;
    using Switchboard.Core.Settings;
    using Switchboard.Core.Telemetry;
    using Test.Shared.Harness;
    using Touchstone.Core;

    /// <summary>
    /// Integration suite for telemetry. Each case boots a real <see cref="SwitchboardDaemon"/> with
    /// telemetry enabled in front of live <see cref="OriginHost"/> backends on random ports, then attaches
    /// an in-process <see cref="MeterListener"/> or <see cref="ActivityListener"/> to the shared
    /// <see cref="SwitchboardTelemetry"/> meter/source to assert that the hot path emits the documented
    /// metrics and spans. The OTLP providers are disabled for the metric/trace assertion cases (the
    /// listeners observe the instruments directly, needing no collector), and a dedicated smoke case
    /// exercises the real provider build/dispose path. Positive and negative assertions are included.
    /// </summary>
    public static class TelemetrySuites
    {
        /// <summary>
        /// All telemetry integration suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the telemetry integration suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Telemetry",
                displayName: "Telemetry (integration)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Telemetry", "RequestCountersIncrement", "Proxied requests increment request, duration, and body-size metrics",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: EnableTelemetry(false, true));
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                using (MetricCollector collector = new MetricCollector())
                                {
                                    for (int i = 0; i < 15; i++)
                                        await SendAsync(harness, "/unauthenticated").ConfigureAwait(false);

                                    await WaitForAsync(() => collector.Sum("switchboard_requests_total", "code", "200") >= 15, ct).ConfigureAwait(false);
                                    Check.Equal(15.0, collector.Sum("switchboard_requests_total", "code", "200"), "requests_total rose by 15 for 200s");
                                    Check.True(collector.Count("switchboard_request_duration_seconds") >= 15, "duration histogram recorded per request");
                                    Check.True(collector.Count("switchboard_request_body_bytes") >= 15, "request body histogram recorded");
                                    Check.True(collector.Count("switchboard_response_body_bytes") >= 15, "response body histogram recorded");
                                }
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("Telemetry", "GatewayRejectionsCounted", "Gateway rejections increment the rejection counter; clean requests do not",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: EnableTelemetry(false, true));
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                using (MetricCollector collector = new MetricCollector())
                                {
                                    for (int i = 0; i < 5; i++)
                                        await SendAsync(harness, "/telemetry-no-such-route").ConfigureAwait(false); // 400, no endpoint
                                    for (int i = 0; i < 5; i++)
                                        await SendAsync(harness, "/unauthenticated").ConfigureAwait(false); // 200

                                    await WaitForAsync(() => collector.Sum("switchboard_gateway_rejections_total", "reason", "400") >= 5, ct).ConfigureAwait(false);
                                    Check.Equal(5.0, collector.Sum("switchboard_gateway_rejections_total", "reason", "400"), "five 400 rejections counted");
                                    Check.Equal(0.0, collector.Sum("switchboard_gateway_rejections_total", "reason", "200"), "successful requests are never counted as rejections");
                                }
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("Telemetry", "OriginGaugesAndBuildInfo", "Observable gauges report per-origin state, config counts, and build info",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: EnableTelemetry(false, true));
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                using (MetricCollector collector = new MetricCollector())
                                {
                                    collector.PullObservable();

                                    List<Sample> up = collector.Where("switchboard_origin_up");
                                    Check.Equal(2, up.Count, "one up-gauge per configured origin");
                                    Check.True(up.All(s => s.Value == 1.0), "both origins report healthy (up=1)");
                                    Check.True(up.Any(s => s.Tag("origin") == harness.Settings.Origins[0].Identifier), "origin label matches the configured identifier");

                                    Check.Equal(2.0, collector.Where("switchboard_config_origins").Single().Value, "config_origins reflects two origins");
                                    Check.Equal(1.0, collector.Where("switchboard_build_info").Single().Value, "build_info is 1");
                                    Check.Equal(Constants.SoftwareVersion, collector.Where("switchboard_build_info").Single().Tag("version"), "build_info carries the version label");
                                }
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("Telemetry", "OriginRequestsAndHealthChecks", "Per-origin request and health-check counters increment",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: EnableTelemetry(false, true));
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                using (MetricCollector collector = new MetricCollector())
                                {
                                    for (int i = 0; i < 10; i++)
                                        await SendAsync(harness, "/unauthenticated").ConfigureAwait(false);

                                    await WaitForAsync(() => collector.Sum("switchboard_origin_requests_total") >= 10, ct).ConfigureAwait(false);
                                    Check.True(collector.Sum("switchboard_origin_requests_total") >= 10, "origin_requests_total counted the proxied requests");

                                    // Health probes fire on the 1s interval; wait for at least one within the window.
                                    await WaitForAsync(() => collector.Count("switchboard_origin_health_checks_total") >= 1, ct, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                                    Check.True(collector.Where("switchboard_origin_health_checks_total").Any(s => s.Tag("result") == "success"), "a successful health-check probe was counted");
                                }
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("Telemetry", "SpanPerProxiedRequest", "A proxied request produces one span with the expected attributes; a rejected request produces none",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: EnableTelemetry(false, true));
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                using (SpanCollector spans = new SpanCollector())
                                {
                                    RequestOutcome outcome = await SendAsync(harness, "/unauthenticated").ConfigureAwait(false);
                                    await WaitForAsync(() => spans.Named("proxy test-endpoint").Count >= 1, ct).ConfigureAwait(false);

                                    Activity span = spans.Named("proxy test-endpoint").First();
                                    Check.Equal("test-endpoint", TagValue(span, "switchboard.endpoint"), "span carries the endpoint attribute");
                                    Check.Equal("GET", TagValue(span, "http.request.method"), "span carries the method attribute");
                                    Check.Equal(outcome.OriginName, TagValue(span, "switchboard.origin"), "span origin attribute matches the serving origin");

                                    int before = spans.Named("proxy test-endpoint").Count;
                                    await SendAsync(harness, "/telemetry-no-such-route").ConfigureAwait(false); // 400, never reaches an origin
                                    await Task.Delay(300, ct).ConfigureAwait(false);
                                    Check.Equal(before, spans.Named("proxy test-endpoint").Count, "a rejected request creates no proxy span");
                                }
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("Telemetry", "TraceparentPropagatedToOrigin", "The active trace context is injected onto the forwarded request when propagation is enabled",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: EnableTelemetry(false, true));
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                using (SpanCollector spans = new SpanCollector())
                                {
                                    RequestOutcome outcome = await SendAsync(harness, "/unauthenticated").ConfigureAwait(false);
                                    OriginHost served = harness.Origins.First(o => o.ServerName == outcome.OriginName);
                                    string? traceparent = served.LastRequestHeader("traceparent");
                                    Check.True(!String.IsNullOrEmpty(traceparent), "origin received a traceparent header");
                                    Check.True(traceparent!.StartsWith("00-", StringComparison.Ordinal), "traceparent uses the W3C version-00 format");
                                }
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("Telemetry", "NoTraceContextWhenTracingInactive", "With no span being recorded, no trace context reaches the origin",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: EnableTelemetry(false, true));
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                // No SpanCollector and providers off: nothing samples the Switchboard source, so no
                                // Activity is current during the proxy and neither Switchboard nor the runtime
                                // propagates a trace context downstream.
                                RequestOutcome outcome = await SendAsync(harness, "/unauthenticated").ConfigureAwait(false);
                                OriginHost served = harness.Origins.First(o => o.ServerName == outcome.OriginName);
                                Check.True(String.IsNullOrEmpty(served.LastRequestHeader("traceparent")), "no traceparent header when no trace is active");
                            }
                            finally { harness.Dispose(); }
                        }),

                    new TestCaseDescriptor("Telemetry", "ProvidersBuildAndDisposeCleanly", "Enabling the OTLP providers builds and disposes without error even with no collector present",
                        executeAsync: async ct =>
                        {
                            ProxyHarness harness = new ProxyHarness(Origins(2), configure: EnableTelemetry(true, true));
                            try
                            {
                                await harness.StartAsync(ct).ConfigureAwait(false);
                                for (int i = 0; i < 3; i++)
                                    await SendAsync(harness, "/unauthenticated").ConfigureAwait(false);
                            }
                            finally { harness.Dispose(); }

                            Check.True(true, "telemetry providers built, served traffic, and disposed without throwing");
                        })
                });
        }

        #region Private-Helpers

        private static Action<SwitchboardSettings> EnableTelemetry(bool buildProviders, bool propagate)
        {
            return settings =>
            {
                settings.Telemetry.Enable = true;
                settings.Telemetry.Metrics.Enable = buildProviders;
                settings.Telemetry.Traces.Enable = buildProviders;
                settings.Telemetry.Logs.Enable = false;
                settings.Telemetry.Traces.PropagateToOrigin = propagate;
                // Keep any accidental export attempts short so provider disposal is never slow.
                settings.Telemetry.Otlp.TimeoutMs = 1000;
            };
        }

        private static IReadOnlyList<string> Origins(int count)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < count; i++)
                names.Add("Server " + (i + 1));
            return names;
        }

        private static async Task<RequestOutcome> SendAsync(ProxyHarness harness, string path)
        {
            using (RestRequest req = new RestRequest(harness.Url(path)))
            using (RestResponse resp = await req.SendAsync().ConfigureAwait(false))
            {
                RequestOutcome outcome = new RequestOutcome();
                outcome.Status = resp.StatusCode;
                outcome.OriginName = resp.Headers != null ? resp.Headers.Get("X-Origin-Server") : null;
                return outcome;
            }
        }

        private static async Task WaitForAsync(Func<bool> condition, CancellationToken token)
        {
            await WaitForAsync(condition, token, TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }

        private static async Task WaitForAsync(Func<bool> condition, CancellationToken token, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) return;
                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }

        private static string? TagValue(Activity activity, string key)
        {
            object? value = activity.GetTagItem(key);
            return value?.ToString();
        }

        private sealed class RequestOutcome
        {
            public int Status { get; set; }
            public string? OriginName { get; set; }
        }

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

        // Accumulates Switchboard meter measurements from any thread. Callbacks fire on the request
        // threads, so all access to the sample list is locked.
        private sealed class MetricCollector : IDisposable
        {
            private readonly MeterListener _Listener;
            private readonly List<Sample> _Samples = new List<Sample>();
            private readonly object _Lock = new object();

            public MetricCollector()
            {
                _Listener = new MeterListener();
                _Listener.InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == SwitchboardTelemetry.SourceName) l.EnableMeasurementEvents(instrument);
                };
                _Listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => Add(instrument.Name, measurement, tags));
                _Listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) => Add(instrument.Name, measurement, tags));
                _Listener.Start();
            }

            public void PullObservable()
            {
                _Listener.RecordObservableInstruments();
            }

            public double Sum(string name)
            {
                lock (_Lock) return _Samples.Where(s => s.Name == name).Sum(s => s.Value);
            }

            public double Sum(string name, string tagKey, string tagValue)
            {
                lock (_Lock) return _Samples.Where(s => s.Name == name && s.Tag(tagKey) == tagValue).Sum(s => s.Value);
            }

            public int Count(string name)
            {
                lock (_Lock) return _Samples.Count(s => s.Name == name);
            }

            public List<Sample> Where(string name)
            {
                lock (_Lock) return _Samples.Where(s => s.Name == name).ToList();
            }

            private void Add(string name, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
            {
                Sample sample = new Sample { Name = name, Value = value };
                foreach (KeyValuePair<string, object?> tag in tags)
                    sample.Tags[tag.Key] = tag.Value?.ToString() ?? "";
                lock (_Lock) _Samples.Add(sample);
            }

            public void Dispose()
            {
                _Listener.Dispose();
            }
        }

        // Collects stopped Switchboard activities (spans) for assertion.
        private sealed class SpanCollector : IDisposable
        {
            private readonly ActivityListener _Listener;
            private readonly List<Activity> _Activities = new List<Activity>();
            private readonly object _Lock = new object();

            public SpanCollector()
            {
                _Listener = new ActivityListener();
                _Listener.ShouldListenTo = source => source.Name == SwitchboardTelemetry.SourceName;
                _Listener.Sample = SampleAll;
                _Listener.ActivityStopped = activity =>
                {
                    lock (_Lock) _Activities.Add(activity);
                };
                ActivitySource.AddActivityListener(_Listener);
            }

            public List<Activity> Named(string operationName)
            {
                lock (_Lock) return _Activities.Where(a => a.OperationName == operationName).ToList();
            }

            private static ActivitySamplingResult SampleAll(ref ActivityCreationOptions<ActivityContext> options)
            {
                return ActivitySamplingResult.AllDataAndRecorded;
            }

            public void Dispose()
            {
                _Listener.Dispose();
            }
        }

        #endregion
    }
}
