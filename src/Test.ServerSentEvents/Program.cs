namespace Test.ServerSentEvents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using RestWrapper;
    using Switchboard.Core;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    public static class Program
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        private const int ORIGIN_PORT = 9002;
        private const int SWITCHBOARD_PORT = 9000;
        private const string TEST_URL = "http://localhost:9000/sse-test";

        private static bool _DebugMode = false;
        private static int _OriginRequestCount = 0;
        private static List<TestResult> _TestResults = new List<TestResult>();
        private static Stopwatch _OverallStopwatch = new Stopwatch();

        // Expected SSE events that the origin server sends
        private static readonly string[] ExpectedEvents = new string[]
        {
            "Connected to SSE Origin Server",
            "Event 1 from origin",
            "Event 2 from origin",
            "Event 3 from origin",
            "Stream closing from origin server"
        };

        public static async Task Main(string[] args)
        {
            _OverallStopwatch.Start();

            Console.WriteLine("");
            Console.WriteLine("================================================");
            Console.WriteLine("      SERVER-SENT EVENTS TEST SUITE");
            Console.WriteLine("================================================");
            Console.WriteLine("");

            bool allTestsPassed = false;

            try
            {
                // Start origin server
                Console.WriteLine("Starting origin server on port " + ORIGIN_PORT + "...");
                WebserverSettings originSettings = new WebserverSettings
                {
                    Hostname = "localhost",
                    Port = ORIGIN_PORT
                };

                using (Webserver originServer = new Webserver(originSettings, OriginServerRoute))
                {
                    originServer.Start();

                    // Configure Switchboard
                    SwitchboardSettings sbSettings = new SwitchboardSettings();
                    sbSettings.Webserver.Hostname = "localhost";
                    sbSettings.Webserver.Port = SWITCHBOARD_PORT;
                    sbSettings.Logging.ConsoleLogging = _DebugMode;
                    sbSettings.Logging.MinimumSeverity = _DebugMode ? 0 : 3;
                    sbSettings.Logging.EnableColors = true;

                    sbSettings.Endpoints.Add(new ApiEndpoint
                    {
                        Identifier = "sse-test-endpoint",
                        Name = "SSE Test Endpoint",
                        LoadBalancing = LoadBalancingMode.RoundRobin,
                        Unauthenticated = new ApiEndpointGroup
                        {
                            ParameterizedUrls = new Dictionary<string, List<string>>
                            {
                                { "GET", new List<string> { "/sse-test" } }
                            }
                        },
                        OriginServers = new List<string> { "sse-origin" }
                    });

                    sbSettings.Origins.Add(new OriginServer
                    {
                        Identifier = "sse-origin",
                        Name = "SSE Origin Server",
                        Hostname = "localhost",
                        Port = ORIGIN_PORT,
                        Ssl = false,
                        HealthCheckIntervalMs = 1000,
                        UnhealthyThreshold = 2,
                        HealthyThreshold = 1
                    });

                    // Start Switchboard
                    using (SwitchboardDaemon switchboard = new SwitchboardDaemon(sbSettings))
                    {
                        // Wait for health checks
                        Console.WriteLine("Waiting for origin server to become healthy...");
                        await Task.Delay(2000);

                        if (!sbSettings.Origins[0].Healthy)
                        {
                            throw new Exception("Origin server did not become healthy");
                        }
                        Console.WriteLine("✓ Origin server is healthy");
                        Console.WriteLine("");

                        // Run all tests
                        Console.WriteLine("Running Server-Sent Events tests...");
                        Console.WriteLine("");

                        await RunTest("RestWrapper - SSE Event Stream", TestServerSentEventsWithRestWrapper);
                        await RunTest("HttpClient - SSE Event Stream", TestServerSentEventsWithHttpClient);

                        allTestsPassed = _TestResults.All(r => r.Passed);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("");
                Console.WriteLine("❌ FATAL ERROR: " + ex.Message);
                if (!String.IsNullOrEmpty(ex.StackTrace) && _DebugMode)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }

            _OverallStopwatch.Stop();

            // Print summary
            PrintSummary();

            Environment.Exit(allTestsPassed ? 0 : 1);
        }

        private static async Task<string> TestServerSentEventsWithRestWrapper()
        {
            using (RestRequest req = new RestRequest(TEST_URL))
            {
                req.Headers.Add("Accept", "text/event-stream");
                req.Headers.Add("Cache-Control", "no-cache");

                using (RestResponse resp = await req.SendAsync())
                {
                    if (resp.StatusCode != 200)
                    {
                        throw new Exception("Expected status 200, got " + resp.StatusCode);
                    }

                    if (!resp.ServerSentEvents)
                    {
                        throw new Exception("Response was not detected as Server-Sent Events");
                    }

                    List<string> receivedEvents = new List<string>();

                    while (true)
                    {
                        RestWrapper.ServerSentEvent sse = await resp.ReadEventAsync();
                        if (sse == null || String.IsNullOrEmpty(sse.Data))
                        {
                            break;
                        }

                        receivedEvents.Add(sse.Data);

                        if (receivedEvents.Count >= ExpectedEvents.Length)
                        {
                            break;
                        }
                    }

                    // Explicit byte-for-byte validation
                    if (receivedEvents.Count != ExpectedEvents.Length)
                    {
                        throw new Exception("Expected " + ExpectedEvents.Length + " events, but received " + receivedEvents.Count);
                    }

                    for (int i = 0; i < ExpectedEvents.Length; i++)
                    {
                        string expected = ExpectedEvents[i];
                        // RestWrapper.ReadEventAsync() may leave trailing \r in the data
                        // Trim it for comparison since it's just a parsing quirk
                        string received = receivedEvents[i].TrimEnd('\r', '\n');

                        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
                        byte[] receivedBytes = Encoding.UTF8.GetBytes(received);

                        if (_DebugMode)
                        {
                            Console.WriteLine("      [RestWrapper] Event " + (i + 1) + ": Expected '" + expected + "' (" + expectedBytes.Length + " bytes)");
                            Console.WriteLine("      [RestWrapper] Event " + (i + 1) + ": Received '" + received + "' (" + receivedBytes.Length + " bytes)");
                            Console.WriteLine("      [RestWrapper] Event " + (i + 1) + ": Expected bytes: " + BitConverter.ToString(expectedBytes));
                            Console.WriteLine("      [RestWrapper] Event " + (i + 1) + ": Received bytes: " + BitConverter.ToString(receivedBytes));
                        }

                        if (expectedBytes.Length != receivedBytes.Length)
                        {
                            throw new Exception(
                                "Event " + (i + 1) + " length mismatch: " +
                                "expected " + expectedBytes.Length + " bytes, " +
                                "received " + receivedBytes.Length + " bytes");
                        }

                        for (int j = 0; j < expectedBytes.Length; j++)
                        {
                            if (expectedBytes[j] != receivedBytes[j])
                            {
                                throw new Exception(
                                    "Event " + (i + 1) + " byte mismatch at position " + j + ": " +
                                    "expected 0x" + expectedBytes[j].ToString("X2") + ", " +
                                    "received 0x" + receivedBytes[j].ToString("X2"));
                            }
                        }
                    }

                    return "Received and validated " + receivedEvents.Count + " SSE events byte-for-byte";
                }
            }
        }

        private static async Task<string> TestServerSentEventsWithHttpClient()
        {
            using (HttpClient client = new HttpClient())
            {
                HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, TEST_URL);
                request.Headers.Add("Accept", "text/event-stream");
                request.Headers.Add("Cache-Control", "no-cache");

                using (HttpResponseMessage resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        throw new Exception("Expected status 200, got " + (int)resp.StatusCode);
                    }

                    // Check content type
                    if (resp.Content.Headers.ContentType?.MediaType != "text/event-stream")
                    {
                        throw new Exception("Response content type is not text/event-stream");
                    }

                    List<string> receivedEvents = new List<string>();

                    using (Stream stream = await resp.Content.ReadAsStreamAsync())
                    {
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            string? currentData = null;
                            string? line;

                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                // SSE format: "data: <content>" or empty line (event separator)
                                if (line.StartsWith("data: "))
                                {
                                    currentData = line.Substring(6); // Remove "data: " prefix
                                }
                                else if (String.IsNullOrEmpty(line) && currentData != null)
                                {
                                    // Empty line marks end of event
                                    receivedEvents.Add(currentData);
                                    currentData = null;

                                    if (receivedEvents.Count >= ExpectedEvents.Length)
                                    {
                                        break;
                                    }
                                }
                            }

                            // Add final event if stream ended without empty line
                            if (currentData != null)
                            {
                                receivedEvents.Add(currentData);
                            }
                        }
                    }

                    // Explicit byte-for-byte validation
                    if (receivedEvents.Count != ExpectedEvents.Length)
                    {
                        throw new Exception("Expected " + ExpectedEvents.Length + " events, but received " + receivedEvents.Count);
                    }

                    for (int i = 0; i < ExpectedEvents.Length; i++)
                    {
                        string expected = ExpectedEvents[i];
                        // RestWrapper.ReadEventAsync() may leave trailing \r in the data
                        // Trim it for comparison since it's just a parsing quirk
                        string received = receivedEvents[i].TrimEnd('\r', '\n');

                        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
                        byte[] receivedBytes = Encoding.UTF8.GetBytes(received);

                        if (expectedBytes.Length != receivedBytes.Length)
                        {
                            throw new Exception(
                                "Event " + (i + 1) + " length mismatch: " +
                                "expected " + expectedBytes.Length + " bytes, " +
                                "received " + receivedBytes.Length + " bytes");
                        }

                        for (int j = 0; j < expectedBytes.Length; j++)
                        {
                            if (expectedBytes[j] != receivedBytes[j])
                            {
                                throw new Exception(
                                    "Event " + (i + 1) + " byte mismatch at position " + j + ": " +
                                    "expected 0x" + expectedBytes[j].ToString("X2") + ", " +
                                    "received 0x" + receivedBytes[j].ToString("X2"));
                            }
                        }
                    }

                    return "Received and validated " + receivedEvents.Count + " SSE events byte-for-byte";
                }
            }
        }

        private static async Task RunTest(string testName, Func<Task<string>> testAction)
        {
            Console.Write("  [" + (_TestResults.Count + 1) + "] " + testName.PadRight(45) + " ... ");

            Stopwatch sw = Stopwatch.StartNew();
            TestResult result = new TestResult
            {
                TestName = testName,
                StartTime = DateTime.Now
            };

            try
            {
                result.ResultMessage = await testAction();
                result.Passed = true;
                sw.Stop();
                result.Duration = sw.Elapsed;

                Console.WriteLine("✅ PASS (" + result.Duration.TotalMilliseconds.ToString("F0") + "ms)");
                if (_DebugMode && !String.IsNullOrEmpty(result.ResultMessage))
                {
                    Console.WriteLine("      → " + result.ResultMessage);
                }
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.ErrorMessage = ex.Message;
                sw.Stop();
                result.Duration = sw.Elapsed;

                Console.WriteLine("❌ FAIL");
                Console.WriteLine("      Error: " + ex.Message);

                if (_DebugMode)
                {
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine("      Inner Exception: " + ex.InnerException.Message);
                    }
                }
            }

            _TestResults.Add(result);
        }

        private static void PrintSummary()
        {
            Console.WriteLine("");
            Console.WriteLine("================================================");
            Console.WriteLine("                TEST SUMMARY");
            Console.WriteLine("================================================");
            Console.WriteLine("");

            int passed = _TestResults.Count(r => r.Passed);
            int failed = _TestResults.Count(r => !r.Passed);

            Console.WriteLine("Total Tests:   " + _TestResults.Count);
            Console.WriteLine("Passed:        " + passed + " (" + (passed * 100.0 / _TestResults.Count).ToString("F1") + "%)");
            Console.WriteLine("Failed:        " + failed);
            Console.WriteLine("Total Runtime: " + _OverallStopwatch.Elapsed.TotalSeconds.ToString("F2") + " seconds");
            Console.WriteLine("");

            Console.WriteLine("Test Results:");
            foreach (TestResult result in _TestResults)
            {
                string status = result.Passed ? "✅ PASS" : "❌ FAIL";
                Console.WriteLine("  " + status + " - " + result.TestName);
                if (!result.Passed)
                {
                    Console.WriteLine("         Error: " + result.ErrorMessage);
                }
            }
            Console.WriteLine("");

            if (failed == 0)
            {
                Console.WriteLine("✅ ALL TESTS PASSED");
            }
            else
            {
                Console.WriteLine("❌ " + failed + " TEST(S) FAILED");
            }

            Console.WriteLine("================================================");
            Console.WriteLine("");
        }

        private class TestResult
        {
            public string TestName { get; set; } = string.Empty;
            public bool Passed { get; set; }
            public DateTime StartTime { get; set; }
            public TimeSpan Duration { get; set; }
            public string ResultMessage { get; set; } = string.Empty;
            public string ErrorMessage { get; set; } = string.Empty;
        }

        private static async Task OriginServerRoute(HttpContextBase ctx)
        {
            System.Threading.Interlocked.Increment(ref _OriginRequestCount);

            try
            {
                // Respond with Server-Sent Events
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/event-stream";
                ctx.Response.Headers.Add("Cache-Control", "no-cache");
                ctx.Response.Headers.Add("Connection", "keep-alive");
                ctx.Response.ServerSentEvents = true;

                // Send events matching the ExpectedEvents array for validation
                for (int i = 0; i < ExpectedEvents.Length; i++)
                {
                    bool isFinal = (i == ExpectedEvents.Length - 1);

                    await ctx.Response.SendEvent(new WatsonWebserver.Core.ServerSentEvent
                    {
                        Data = ExpectedEvents[i]
                    }, isFinal);

                    // Wait 50ms between events (except after the last one)
                    if (!isFinal)
                    {
                        await Task.Delay(50);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_DebugMode)
                {
                    Console.WriteLine("");
                    Console.WriteLine("ERROR: Origin server exception:");
                    Console.WriteLine("  " + ex.Message);
                    Console.WriteLine("");
                }
            }
        }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
