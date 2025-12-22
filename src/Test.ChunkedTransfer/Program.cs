namespace Test.ChunkedTransfer
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

        private const int ORIGIN_PORT = 9001;
        private const int SWITCHBOARD_PORT = 9000;
        private const string TEST_URL = "http://localhost:9000/chunked-test";

        private static bool _DebugMode = false;
        private static int _OriginRequestCount = 0;
        private static List<TestResult> _TestResults = new List<TestResult>();
        private static Stopwatch _OverallStopwatch = new Stopwatch();

        // Expected chunks that the origin server sends
        private static readonly string[] ExpectedChunks = new string[]
        {
            "Chunk 1: This is the first chunk of data.\n",
            "Chunk 2: Here comes the second chunk.\n",
            "Chunk 3: Third chunk with more data.\n",
            "Chunk 4: Final chunk to complete the transfer.\n"
        };

        public static async Task Main(string[] args)
        {
            _OverallStopwatch.Start();

            Console.WriteLine("");
            Console.WriteLine("================================================");
            Console.WriteLine("   CHUNKED TRANSFER ENCODING TEST SUITE");
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
                        Identifier = "chunked-test-endpoint",
                        Name = "Chunked Test Endpoint",
                        LoadBalancing = LoadBalancingMode.RoundRobin,
                        Unauthenticated = new ApiEndpointGroup
                        {
                            ParameterizedUrls = new Dictionary<string, List<string>>
                            {
                                { "GET", new List<string> { "/chunked-test" } }
                            }
                        },
                        OriginServers = new List<string> { "chunked-origin" }
                    });

                    sbSettings.Origins.Add(new OriginServer
                    {
                        Identifier = "chunked-origin",
                        Name = "Chunked Origin Server",
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
                        Console.WriteLine("Running chunked transfer tests...");
                        Console.WriteLine("");

                        await RunTest("RestWrapper - Chunked Transfer", TestWithRestWrapper);
                        await RunTest("HttpClient - Chunked Transfer", TestWithHttpClient);

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

        private static async Task OriginServerRoute(HttpContextBase ctx)
        {
            System.Threading.Interlocked.Increment(ref _OriginRequestCount);

            try
            {
                // Respond with chunked transfer encoding
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                ctx.Response.ChunkedTransfer = true;

                for (int i = 0; i < ExpectedChunks.Length; i++)
                {
                    byte[] chunkData = Encoding.UTF8.GetBytes(ExpectedChunks[i]);
                    bool isFinal = (i == ExpectedChunks.Length - 1);

                    await ctx.Response.SendChunk(chunkData, isFinal);

                    // Wait 50ms between chunks (except after the last one)
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

        private static async Task<string> TestWithRestWrapper()
        {
            using (RestRequest req = new RestRequest(TEST_URL))
            {
                using (RestResponse resp = await req.SendAsync())
                {
                    if (resp.StatusCode != 200)
                    {
                        throw new Exception("Expected status 200, got " + resp.StatusCode);
                    }

                    if (!resp.ChunkedTransferEncoding)
                    {
                        throw new Exception("Expected chunked transfer encoding, but response is not chunked");
                    }

                    List<byte[]> receivedChunks = new List<byte[]>();

                    while (true)
                    {
                        RestWrapper.ChunkData chunk = await resp.ReadChunkAsync();
                        if (chunk == null || chunk.Data == null || chunk.Data.Length == 0) break;
                        receivedChunks.Add(chunk.Data);
                    }

                    // Explicit byte-for-byte validation
                    if (receivedChunks.Count != ExpectedChunks.Length)
                    {
                        throw new Exception("Expected " + ExpectedChunks.Length + " chunks, but received " + receivedChunks.Count);
                    }

                    for (int i = 0; i < ExpectedChunks.Length; i++)
                    {
                        // RestWrapper.ReadChunkAsync uses ReadLineAsync which strips line endings
                        string expectedContent = ExpectedChunks[i].TrimEnd('\n', '\r');
                        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedContent);
                        byte[] receivedBytes = receivedChunks[i];

                        if (expectedBytes.Length != receivedBytes.Length)
                        {
                            throw new Exception(
                                "Chunk " + (i + 1) + " length mismatch: " +
                                "expected " + expectedBytes.Length + " bytes, " +
                                "received " + receivedBytes.Length + " bytes");
                        }

                        for (int j = 0; j < expectedBytes.Length; j++)
                        {
                            if (expectedBytes[j] != receivedBytes[j])
                            {
                                throw new Exception(
                                    "Chunk " + (i + 1) + " byte mismatch at position " + j + ": " +
                                    "expected 0x" + expectedBytes[j].ToString("X2") + ", " +
                                    "received 0x" + receivedBytes[j].ToString("X2"));
                            }
                        }
                    }

                    return "Received and validated " + receivedChunks.Count + " chunks byte-for-byte";
                }
            }
        }

        private static async Task<string> TestWithHttpClient()
        {
            using (HttpClient client = new HttpClient())
            {
                using (HttpResponseMessage resp = await client.GetAsync(TEST_URL, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        throw new Exception("Expected status 200, got " + (int)resp.StatusCode);
                    }

                    // Check transfer encoding
                    bool isChunked = false;
                    if (resp.Headers.TransferEncodingChunked.HasValue)
                    {
                        isChunked = resp.Headers.TransferEncodingChunked.Value;
                    }

                    if (!isChunked)
                    {
                        throw new Exception("Expected chunked transfer encoding, but response is not chunked");
                    }

                    List<byte[]> receivedChunks = new List<byte[]>();

                    using (Stream stream = await resp.Content.ReadAsStreamAsync())
                    {
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            string? line;
                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                byte[] chunkBytes = Encoding.UTF8.GetBytes(line + "\n");
                                receivedChunks.Add(chunkBytes);
                            }
                        }
                    }

                    // Explicit byte-for-byte validation
                    if (receivedChunks.Count != ExpectedChunks.Length)
                    {
                        throw new Exception("Expected " + ExpectedChunks.Length + " chunks, but received " + receivedChunks.Count);
                    }

                    for (int i = 0; i < ExpectedChunks.Length; i++)
                    {
                        byte[] expectedBytes = Encoding.UTF8.GetBytes(ExpectedChunks[i]);
                        byte[] receivedBytes = receivedChunks[i];

                        if (expectedBytes.Length != receivedBytes.Length)
                        {
                            throw new Exception(
                                "Chunk " + (i + 1) + " length mismatch: " +
                                "expected " + expectedBytes.Length + " bytes, " +
                                "received " + receivedBytes.Length + " bytes");
                        }

                        for (int j = 0; j < expectedBytes.Length; j++)
                        {
                            if (expectedBytes[j] != receivedBytes[j])
                            {
                                throw new Exception(
                                    "Chunk " + (i + 1) + " byte mismatch at position " + j + ": " +
                                    "expected 0x" + expectedBytes[j].ToString("X2") + ", " +
                                    "received 0x" + receivedBytes[j].ToString("X2"));
                            }
                        }
                    }

                    return "Received and validated " + receivedChunks.Count + " chunks byte-for-byte";
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

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
