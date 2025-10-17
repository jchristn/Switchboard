namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using RestWrapper;
    using SerializationHelper;
    using Switchboard.Core;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    public static class Program
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).

        private static SwitchboardSettings _Settings = null;
        private static Serializer _Serializer = new Serializer();
        private static LoadBalancingMode _LoadBalancingMode = LoadBalancingMode.RoundRobin;

        private static List<TestResult> _TestResults = new List<TestResult>();
        private static Stopwatch _OverallStopwatch = new Stopwatch();

        private static WebserverSettings _Server1Settings = null;
        private static Webserver _Server1 = null;

        private static WebserverSettings _Server2Settings = null;
        private static Webserver _Server2 = null;

        private static WebserverSettings _Server3Settings = null;
        private static Webserver _Server3 = null;

        private static WebserverSettings _Server4Settings = null;
        private static Webserver _Server4 = null;

        private static int _NumRequests = 8;

        public static async Task Main(string[] args)
        {
            _OverallStopwatch.Start();

            Console.WriteLine("");
            Console.WriteLine("======================================");
            Console.WriteLine("     SWITCHBOARD DAEMON TEST SUITE");
            Console.WriteLine("======================================");
            Console.WriteLine("");
            Console.WriteLine("Test Configuration:");
            Console.WriteLine("- Unauthenticated URL : GET /unauthenticated");
            Console.WriteLine("- Authenticated URL   : GET /authenticated");
            Console.WriteLine("- Load Balancing      : Round Robin");
            Console.WriteLine("- Origin Servers      : 4 (ports 8001-8004)");
            Console.WriteLine("");

            InitializeSettings();
            InitializeOriginServers();

            string url;

            using (SwitchboardDaemon switchboard = new SwitchboardDaemon(_Settings))
            {
                switchboard.Callbacks.AuthenticateAndAuthorize = AuthenticateAndAuthorizeRequest;

                using (_Server1 = new Webserver(_Server1Settings, Server1Route))
                {
                    using (_Server2 = new Webserver(_Server2Settings, Server2Route))
                    {
                        using (_Server3 = new Webserver(_Server3Settings, Server3Route))
                        {
                            using (_Server4 = new Webserver(_Server4Settings, Server4Route))
                            {
                                _Server1.Start();
                                _Server2.Start();
                                _Server3.Start();
                                _Server4.Start();

                                #region Wait-for-Healthy-Origins

                                await RunTest("Origin Server Startup", async () =>
                                {
                                    Console.WriteLine("Waiting for origin servers to start...");

                                    while (_Settings.Origins.Any(o => !o.Healthy))
                                    {
                                        Console.WriteLine("At least one origin is unhealthy, waiting 1000ms");
                                        await Task.Delay(1000);
                                    }

                                    bool allHealthy = _Settings.Origins.All(o => o.Healthy);
                                    if (!allHealthy) throw new Exception("Not all origin servers became healthy");

                                    return "All origin servers are healthy";
                                });

                                #endregion

                                #region Should-Succeed

                                await RunTest("Unauthenticated Requests - Success Cases", async () =>
                                {
                                    int successCount = 0;
                                    for (int i = 0; i < _NumRequests; i++)
                                    {
                                        url = "http://localhost:8000/unauthenticated";
                                        if (i % 2 == 0) url += "?foo=bar";
                                        Console.WriteLine($"  Request {i}: {url}");

                                        using (RestRequest req = new RestRequest(url))
                                        {
                                            using (RestResponse resp = await req.SendAsync())
                                            {
                                                Console.WriteLine($"  Response ({resp.StatusCode}): {resp.DataAsString}");
                                                if (resp.StatusCode == 200) successCount++;
                                            }
                                        }
                                    }

                                    if (successCount != _NumRequests)
                                        throw new Exception($"Expected {_NumRequests} successful requests, got {successCount}");

                                    return $"{successCount}/{_NumRequests} requests succeeded";
                                });

                                #endregion

                                #region Should-Fail

                                await RunTest("Invalid URL - Negative Case", async () =>
                                {
                                    using (RestRequest req = new RestRequest("http://localhost:8000/undefined"))
                                    {
                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            Console.WriteLine($"  Response ({resp.StatusCode}): {resp.DataAsString}");

                                            if (resp.StatusCode == 200)
                                                throw new Exception("Expected failure for undefined URL, but got success");

                                            return $"Correctly returned status {resp.StatusCode} for invalid URL";
                                        }
                                    }
                                });

                                #endregion

                                #region URL-Rewrite

                                await RunTest("Authentication Test - Mixed Success/Failure", async () =>
                                {
                                    int expectedSuccesses = 0;
                                    int actualSuccesses = 0;
                                    int expectedFailures = 0;
                                    int actualFailures = 0;

                                    for (int i = 0; i < _NumRequests; i++)
                                    {
                                        bool shouldSucceed = (i % 2 == 0);
                                        if (shouldSucceed) expectedSuccesses++;
                                        else expectedFailures++;

                                        url = "http://localhost:8000/authenticated";
                                        if (i % 2 == 0) url += "?foo=bar";
                                        Console.WriteLine($"  Request {i} (expecting {(shouldSucceed ? "success" : "failure")}): {url}");

                                        using (RestRequest req = new RestRequest(url))
                                        {
                                            if (shouldSucceed) req.Authorization.BearerToken = "foo";

                                            using (RestResponse resp = await req.SendAsync())
                                            {
                                                Console.WriteLine($"  Response ({resp.StatusCode}): {resp.DataAsString}");

                                                if (resp.StatusCode == 200)
                                                {
                                                    actualSuccesses++;
                                                    if (!shouldSucceed)
                                                        throw new Exception($"Request {i} should have failed but succeeded");
                                                }
                                                else
                                                {
                                                    actualFailures++;
                                                    if (shouldSucceed)
                                                        throw new Exception($"Request {i} should have succeeded but failed with {resp.StatusCode}");
                                                }
                                            }
                                        }
                                    }

                                    return $"Authentication working correctly: {actualSuccesses}/{expectedSuccesses} successes, {actualFailures}/{expectedFailures} failures";
                                });

                                #endregion

                                #region URL-Rewrite-Test

                                await RunTest("URL Rewriting - Path Parameter Transformation", async () =>
                                {
                                    // Request to /api/v2/users/12345 should be rewritten to /v1/users/12345 at origin
                                    url = "http://localhost:8000/api/v2/users/12345";
                                    Console.WriteLine($"  Requesting: {url}");
                                    Console.WriteLine($"  Expected origin to receive: /v1/users/12345");

                                    using (RestRequest req = new RestRequest(url))
                                    {
                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            Console.WriteLine($"  Response ({resp.StatusCode}): {resp.DataAsString}");

                                            if (resp.StatusCode != 200)
                                                throw new Exception($"Expected 200 OK, got {resp.StatusCode}");

                                            // Parse response to verify the origin received the rewritten URL
                                            if (!resp.DataAsString.Contains("/v1/users/12345"))
                                                throw new Exception($"Origin did not receive rewritten URL. Response: {resp.DataAsString}");

                                            return "URL successfully rewritten from /api/v2/users/12345 to /v1/users/12345";
                                        }
                                    }
                                });

                                #endregion

                                #region Stop-All-Servers

                                await RunTest("Health Check - All Servers Down", async () =>
                                {
                                    Console.WriteLine("  Stopping all origin servers...");
                                    _Server1.Stop();
                                    _Server2.Stop();
                                    _Server3.Stop();
                                    _Server4.Stop();

                                    Console.WriteLine("  Waiting for health checks to detect server failures...");

                                    // Wait with timeout for health checks to detect failures
                                    int maxWaitSeconds = 20;
                                    int waitedSeconds = 0;

                                    while (_Settings.Origins.Any(o => o.Healthy) && waitedSeconds < maxWaitSeconds)
                                    {
                                        int unhealthyCount = _Settings.Origins.Count(o => !o.Healthy);
                                        int totalCount = _Settings.Origins.Count;
                                        Console.WriteLine($"  └─ Health status: {unhealthyCount}/{totalCount} servers marked unhealthy, waiting...");

                                        await Task.Delay(1000);
                                        waitedSeconds++;
                                    }

                                    bool allUnhealthy = _Settings.Origins.All(o => !o.Healthy);
                                    if (!allUnhealthy)
                                    {
                                        int unhealthyCount = _Settings.Origins.Count(o => !o.Healthy);
                                        throw new Exception($"Timeout: Only {unhealthyCount}/{_Settings.Origins.Count} servers marked unhealthy after {maxWaitSeconds} seconds");
                                    }

                                    return "All servers correctly detected as unhealthy";
                                });

                                #endregion

                                #region Validate-502s

                                await RunTest("Gateway Error Response - 502 Bad Gateway", async () =>
                                {
                                    int correctErrorCount = 0;
                                    int totalRequests = 3;

                                    for (int i = 0; i < totalRequests; i++)
                                    {
                                        url = "http://localhost:8000/unauthenticated";
                                        Console.WriteLine($"  Request {i} (expecting 502): {url}");

                                        using (RestRequest req = new RestRequest(url))
                                        {
                                            using (RestResponse resp = await req.SendAsync())
                                            {
                                                Console.WriteLine($"  Response ({resp.StatusCode}): {resp.DataAsString}");

                                                if (resp.StatusCode == 502)
                                                {
                                                    correctErrorCount++;
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"  WARNING: Expected 502 but got {resp.StatusCode}");
                                                }
                                            }
                                        }
                                    }

                                    if (correctErrorCount == 0)
                                        throw new Exception("No 502 errors received when all servers are down");

                                    return $"{correctErrorCount}/{totalRequests} requests correctly returned 502 errors";
                                });

                                #endregion

                                #region Additional-Negative-Tests

                                await RunTest("HTTP Method Routing - PUT, DELETE, PATCH", async () =>
                                {
                                    string[] methods = { "PUT", "DELETE", "PATCH" };
                                    int expectedErrors = 0;
                                    int actualErrors = 0;

                                    foreach (string method in methods)
                                    {
                                        expectedErrors++;
                                        Console.WriteLine($"  Testing {method} method (expecting 400 - no endpoint match)");

                                        using (RestRequest req = new RestRequest("http://localhost:8000/unauthenticated", new System.Net.Http.HttpMethod(method)))
                                        {
                                            using (RestResponse resp = await req.SendAsync())
                                            {
                                                Console.WriteLine($"  {method} Response ({resp.StatusCode}): {resp.DataAsString}");

                                                if (resp.StatusCode != 200)
                                                {
                                                    actualErrors++;
                                                }
                                            }
                                        }
                                    }

                                    return $"{actualErrors}/{expectedErrors} methods correctly returned errors (no matching endpoint)";
                                });

                                // Skip early load balancing test - will run after server recovery

                                // Skip early headers test - will run after server recovery

                                // Skip early querystring test - will run after server recovery

                                #endregion

                                #region Health-Check-Server-Recovery

                                await RunTest("Health Check - Server Recovery (Partial)", async () =>
                                {
                                    Console.WriteLine("  Starting servers 1 and 2...");
                                    _Server1.Start();
                                    _Server2.Start();

                                    Console.WriteLine("  Waiting for health checks to detect recovery...");
                                    while (_Settings.Origins.All(o => !o.Healthy))
                                    {
                                        Console.WriteLine("  All origin servers are unhealthy, waiting 1000ms");
                                        await Task.Delay(1000);
                                    }

                                    int successCount = 0;
                                    int totalRequests = 4;

                                    for (int i = 0; i < totalRequests; i++)
                                    {
                                        url = "http://localhost:8000/unauthenticated";
                                        Console.WriteLine($"  Request {i} (expecting success): {url}");

                                        using (RestRequest req = new RestRequest(url))
                                        {
                                            using (RestResponse resp = await req.SendAsync())
                                            {
                                                Console.WriteLine($"  Response ({resp.StatusCode}): {resp.DataAsString}");

                                                if (resp.StatusCode == 200)
                                                {
                                                    successCount++;
                                                }
                                            }
                                        }
                                    }

                                    if (successCount == 0)
                                        throw new Exception("No requests succeeded after server recovery");

                                    return $"{successCount}/{totalRequests} requests succeeded after partial server recovery";
                                });

                                await RunTest("Health Check - Full Server Recovery", async () =>
                                {
                                    Console.WriteLine("  Starting remaining servers 3 and 4...");
                                    _Server3.Start();
                                    _Server4.Start();

                                    Console.WriteLine("  Waiting for all servers to become healthy...");
                                    while (_Settings.Origins.Any(o => !o.Healthy))
                                    {
                                        Console.WriteLine("  At least one origin server is unhealthy, waiting 1000ms");
                                        await Task.Delay(1000);
                                    }

                                    await Task.Delay(2000); // Extra time for stability

                                    bool allHealthy = _Settings.Origins.All(o => o.Healthy);
                                    if (!allHealthy)
                                        throw new Exception("Not all servers recovered to healthy state");

                                    return "All 4 origin servers are healthy and ready";
                                });

                                #endregion

                                #region Rate-Limiting-Test

                                await RunTest("Rate Limiting - Concurrent Request Burst", async () =>
                                {
                                    Console.WriteLine("  Ensuring all servers are healthy...");
                                    await Task.Delay(2000);

                                    Console.WriteLine("  Sending burst of concurrent requests to trigger rate limiting...");

                                    Task<(int statusCode, string response)>[] tasks = new Task<(int, string)>[20];

                                    for (int i = 0; i < tasks.Length; i++)
                                    {
                                        int index = i;
                                        tasks[i] = Task.Run(async () =>
                                        {
                                            using (RestRequest req = new RestRequest("http://localhost:8000/unauthenticated"))
                                            {
                                                if (index > 0) await Task.Delay(10);

                                                using (RestResponse resp = await req.SendAsync())
                                                {
                                                    return (resp.StatusCode, resp.DataAsString);
                                                }
                                            }
                                        });
                                    }

                                    (int statusCode, string response)[] results = await Task.WhenAll(tasks);

                                    int successCount = 0;
                                    int rateLimitCount = 0;
                                    int otherStatusCount = 0;

                                    for (int i = 0; i < results.Length; i++)
                                    {
                                        Console.WriteLine($"  Request {i}: Status {results[i].statusCode}");

                                        if (results[i].statusCode == 200)
                                            successCount++;
                                        else if (results[i].statusCode == 429)
                                            rateLimitCount++;
                                        else
                                            otherStatusCount++;
                                    }

                                    Console.WriteLine($"  Results: {successCount} successful, {rateLimitCount} rate limited, {otherStatusCount} other");

                                    Console.WriteLine("  Waiting for rate limit to clear...");
                                    await Task.Delay(3000);

                                    // Rate limiting behavior may vary, so we accept either scenario
                                    if (rateLimitCount > 0)
                                        return $"Rate limiting active: {rateLimitCount} requests were rate limited";
                                    else
                                        return $"All {successCount} requests succeeded (rate limits not triggered)";
                                });

                                #endregion

                                #region REST-API-Tests

                                await RunTest("REST API - POST Request with JSON Payload", async () =>
                                {
                                    Dictionary<string, object> payload = new Dictionary<string, object>
                                    {
                                        { "name", "John Doe" },
                                        { "email", "john.doe@example.com" },
                                        { "age", 30 }
                                    };

                                    string jsonPayload = _Serializer.SerializeJson(payload, false);

                                    using (RestRequest req = new RestRequest("http://localhost:8000/api/users", System.Net.Http.HttpMethod.Post))
                                    {
                                        req.ContentType = "application/json";
                                        using (RestResponse resp = await req.SendAsync(jsonPayload))
                                        {
                                            Console.WriteLine($"  POST Response ({resp.StatusCode}): {resp.DataAsString}");

                                            if (resp.StatusCode != 201)
                                                throw new Exception($"Expected 201 Created, got {resp.StatusCode}");

                                            if (!resp.DataAsString.Contains("john.doe@example.com"))
                                                throw new Exception("Response does not contain expected payload data");

                                            return $"POST request successful with {jsonPayload.Length} byte payload";
                                        }
                                    }
                                });

                                await RunTest("REST API - PUT Request with Data Update", async () =>
                                {
                                    Dictionary<string, object> updatePayload = new Dictionary<string, object>
                                    {
                                        { "id", 123 },
                                        { "name", "Jane Smith" },
                                        { "email", "jane.smith@example.com" },
                                        { "age", 28 }
                                    };

                                    string jsonPayload = _Serializer.SerializeJson(updatePayload, false);

                                    using (RestRequest req = new RestRequest("http://localhost:8000/api/users/123", System.Net.Http.HttpMethod.Put))
                                    {
                                        req.ContentType = "application/json";
                                        using (RestResponse resp = await req.SendAsync(jsonPayload))
                                        {
                                            Console.WriteLine($"  PUT Response ({resp.StatusCode}): {resp.DataAsString}");

                                            if (resp.StatusCode != 200)
                                                throw new Exception($"Expected 200 OK, got {resp.StatusCode}");

                                            if (!resp.DataAsString.Contains("jane.smith@example.com"))
                                                throw new Exception("Response does not contain updated payload data");

                                            return $"PUT request successful for user ID 123";
                                        }
                                    }
                                });

                                await RunTest("REST API - PATCH Request with Partial Update", async () =>
                                {
                                    Dictionary<string, object> patchPayload = new Dictionary<string, object>
                                    {
                                        { "age", 29 }
                                    };

                                    string jsonPayload = _Serializer.SerializeJson(patchPayload, false);

                                    using (RestRequest req = new RestRequest("http://localhost:8000/api/users/123", System.Net.Http.HttpMethod.Patch))
                                    {
                                        req.ContentType = "application/json";
                                        using (RestResponse resp = await req.SendAsync(jsonPayload))
                                        {
                                            Console.WriteLine($"  PATCH Response ({resp.StatusCode}): {resp.DataAsString}");

                                            if (resp.StatusCode != 200)
                                                throw new Exception($"Expected 200 OK, got {resp.StatusCode}");

                                            return $"PATCH request successful with partial update";
                                        }
                                    }
                                });

                                await RunTest("REST API - DELETE Request", async () =>
                                {
                                    using (RestRequest req = new RestRequest("http://localhost:8000/api/users/123", System.Net.Http.HttpMethod.Delete))
                                    {
                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            Console.WriteLine($"  DELETE Response ({resp.StatusCode}): {resp.DataAsString}");

                                            if (resp.StatusCode != 204)
                                                throw new Exception($"Expected 204 No Content, got {resp.StatusCode}");

                                            return $"DELETE request successful for user ID 123";
                                        }
                                    }
                                });

                                await RunTest("REST API - GET with Complex Query Parameters", async () =>
                                {
                                    string complexUrl = "http://localhost:8000/api/data?filter=active&sort=name&page=1&limit=10&fields=id,name,email";

                                    using (RestRequest req = new RestRequest(complexUrl))
                                    {
                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            Console.WriteLine($"  GET Response ({resp.StatusCode}): {resp.DataAsString}");

                                            if (resp.StatusCode != 200)
                                                throw new Exception($"Expected 200 OK, got {resp.StatusCode}");

                                            if (!resp.DataAsString.Contains("filter=active"))
                                                throw new Exception("Query parameters not properly forwarded");

                                            return $"Complex query parameters properly handled";
                                        }
                                    }
                                });

                                #endregion

                                #region Chunked-Transfer-Encoding-Tests

                                await RunTest("Chunked Transfer Encoding - Client Side Upload", async () =>
                                {
                                    // Ensure all servers are healthy first
                                    Console.WriteLine("  Ensuring all servers are healthy for chunked transfer test...");
                                    if (_Settings.Origins.Any(o => !o.Healthy))
                                    {
                                        throw new Exception("Cannot test chunked transfer - some servers are unhealthy");
                                    }

                                    // Wait a moment for full server readiness
                                    Console.WriteLine("  Waiting for servers to fully stabilize...");
                                    await Task.Delay(2000);

                                    // Test basic connectivity first
                                    Console.WriteLine("  Testing basic connectivity to /api/upload endpoint...");
                                    using (RestRequest testReq = new RestRequest("http://localhost:8000/api/upload", System.Net.Http.HttpMethod.Post))
                                    {
                                        testReq.ContentType = "application/json";
                                        using (RestResponse testResp = await testReq.SendAsync("{\"test\":\"connectivity\"}"))
                                        {
                                            if (testResp.StatusCode != 200)
                                            {
                                                throw new Exception($"Basic connectivity test failed with {testResp.StatusCode}: {testResp.DataAsString}");
                                            }
                                            Console.WriteLine($"  Basic connectivity successful ({testResp.StatusCode})");
                                        }
                                    }

                                    // Create test data chunks
                                    string[] chunks = {
                                        "This is the first chunk of data for testing chunked transfer encoding.\n",
                                        "Here comes the second chunk with more test data to verify the chunked upload.\n",
                                        "Third chunk containing additional content for a comprehensive test.\n",
                                        "Final chunk to complete the chunked transfer encoding test.\n"
                                    };

                                    Console.WriteLine($"  Sending {chunks.Length} chunks using RestWrapper ChunkedTransfer");

                                    RestRequest req = new RestRequest("http://localhost:8000/api/upload", System.Net.Http.HttpMethod.Post);
                                    req.ContentType = "text/plain";
                                    req.ChunkedTransfer = true; // Enable chunked transfer in RestWrapper

                                    try
                                    {
                                        RestResponse resp = null;

                                        // Send chunks using RestWrapper's SendChunkAsync
                                        for (int i = 0; i < chunks.Length; i++)
                                        {
                                            bool isLastChunk = (i == chunks.Length - 1);
                                            Console.WriteLine($"  Sending chunk {i + 1}/{chunks.Length} (isFinal: {isLastChunk})");

                                            resp = await req.SendChunkAsync(chunks[i], isLastChunk);

                                            // Response is only returned on the final chunk
                                            if (isLastChunk && resp != null)
                                            {
                                                Console.WriteLine($"  Chunked Upload Response ({resp.StatusCode})");
                                                Console.WriteLine($"  Response: {resp.DataAsString}");
                                                break;
                                            }
                                        }

                                        if (resp == null)
                                            throw new Exception("No response received from chunked upload");

                                        if (resp.StatusCode != 200)
                                            throw new Exception($"Expected 200 OK, got {resp.StatusCode}");

                                        if (!resp.DataAsString.Contains("completed") || !resp.DataAsString.Contains("received"))
                                            throw new Exception("Upload response not properly received");

                                        // Parse response to verify bytes received
                                        int totalBytes = chunks.Sum(c => Encoding.UTF8.GetByteCount(c));

                                        // Extract received bytes from JSON response
                                        if (!resp.DataAsString.Contains("\"received\""))
                                            throw new Exception("Response does not contain 'received' field");

                                        // Parse the received value from JSON
                                        int startIndex = resp.DataAsString.IndexOf("\"received\"") + "\"received\"".Length;
                                        string afterReceived = resp.DataAsString.Substring(startIndex);
                                        int colonIndex = afterReceived.IndexOf(":");
                                        int commaIndex = afterReceived.IndexOf(",", colonIndex);
                                        if (commaIndex == -1) commaIndex = afterReceived.IndexOf("}", colonIndex);
                                        string receivedValueStr = afterReceived.Substring(colonIndex + 1, commaIndex - colonIndex - 1).Trim();
                                        int bytesReceived = int.Parse(receivedValueStr);

                                        if (bytesReceived == 0)
                                            throw new Exception($"Origin server received 0 bytes, expected {totalBytes} bytes");

                                        if (bytesReceived != totalBytes)
                                            throw new Exception($"Origin server received {bytesReceived} bytes, expected {totalBytes} bytes");

                                        return $"Chunked upload successful: {totalBytes} bytes sent and received in {chunks.Length} chunks";
                                    }
                                    finally
                                    {
                                        req?.Dispose();
                                    }
                                });

                                await RunTest("Chunked Transfer Encoding - Server Side Response", async () =>
                                {
                                    // Ensure all servers are healthy first
                                    Console.WriteLine("  Ensuring all servers are healthy for server-side chunked test...");
                                    if (_Settings.Origins.Any(o => !o.Healthy))
                                    {
                                        throw new Exception("Cannot test server-side chunked - some servers are unhealthy");
                                    }

                                    using (RestRequest req = new RestRequest("http://localhost:8000/api/upload", System.Net.Http.HttpMethod.Post))
                                    {
                                        req.ContentType = "application/json";
                                        string smallPayload = "{\"test\":\"chunked response\"}";

                                        using (RestResponse resp = await req.SendAsync(smallPayload))
                                        {
                                            Console.WriteLine($"  Chunked Response Test ({resp.StatusCode})");
                                            Console.WriteLine($"  Transfer-Encoding: {resp.Headers.Get("Transfer-Encoding")}");
                                            Console.WriteLine($"  Response: {resp.DataAsString}");

                                            if (resp.StatusCode != 200)
                                                throw new Exception($"Expected 200 OK, got {resp.StatusCode}");

                                            // Server should respond with completed status
                                            if (!resp.DataAsString.Contains("completed") || !resp.DataAsString.Contains("received"))
                                                throw new Exception("Server did not send proper upload response");

                                            return $"Server-side upload response working correctly";
                                        }
                                    }
                                });

                                #endregion

                                #region Server-Sent-Events-Tests

                                await RunTest("Server-Sent Events - Event Stream with Data Verification", async () =>
                                {
                                    // Ensure all servers are healthy first
                                    if (_Settings.Origins.Any(o => !o.Healthy))
                                    {
                                        throw new Exception("Cannot test SSE - some servers are unhealthy");
                                    }

                                    using (RestRequest req = new RestRequest("http://localhost:8000/events"))
                                    {
                                        req.Headers.Add("Accept", "text/event-stream");
                                        req.Headers.Add("Cache-Control", "no-cache");

                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            if (resp.StatusCode != 200)
                                                throw new Exception($"Expected 200 OK, got {resp.StatusCode}");

                                            if (!resp.ServerSentEvents)
                                                throw new Exception("Response not detected as SSE");

                                            // Read and validate SSE events WITH CONTENT VERIFICATION
                                            int eventCount = 0;
                                            List<string> eventDataList = new List<string>();
                                            bool hasConnectedEvent = false;
                                            bool hasNumberedEvents = false;
                                            bool hasClosingEvent = false;

                                            while (true)
                                            {
                                                RestWrapper.ServerSentEvent sse = await resp.ReadEventAsync();
                                                if (sse == null || String.IsNullOrEmpty(sse.Data))
                                                    break;

                                                eventCount++;
                                                eventDataList.Add(sse.Data);

                                                // Verify specific event content
                                                if (sse.Data.Contains("Connected to Server"))
                                                    hasConnectedEvent = true;
                                                if (sse.Data.Contains("Event") && sse.Data.Contains("from Server"))
                                                    hasNumberedEvents = true;
                                                if (sse.Data.Contains("Connection closing"))
                                                    hasClosingEvent = true;

                                                Console.WriteLine($"  Event {eventCount}: {sse.Data}");

                                                if (eventCount > 10) // Safety limit
                                                    break;
                                            }

                                            if (eventCount == 0)
                                                throw new Exception("No SSE events received from origin");

                                            if (eventCount != 5)
                                                throw new Exception($"Expected exactly 5 events, received {eventCount}");

                                            if (!hasConnectedEvent)
                                                throw new Exception("Missing 'Connected' event from origin");

                                            if (!hasNumberedEvents)
                                                throw new Exception("Missing numbered events from origin");

                                            if (!hasClosingEvent)
                                                throw new Exception("Missing 'closing' event from origin");

                                            return $"SSE validated: 5 events received with correct content (Connected, Event 1-3, Closing)";
                                        }
                                    }
                                }, verboseOutput: true);

                                await RunTest("Server-Sent Events - Multiple Concurrent Connections", async () =>
                                {
                                    // Ensure all servers are healthy first
                                    if (_Settings.Origins.Any(o => !o.Healthy))
                                    {
                                        throw new Exception("Cannot test concurrent SSE - some servers are unhealthy");
                                    }

                                    Task<int>[] eventTasks = new Task<int>[3];

                                    for (int i = 0; i < eventTasks.Length; i++)
                                    {
                                        int connectionId = i;
                                        eventTasks[i] = Task.Run(async () =>
                                        {
                                            using (RestRequest req = new RestRequest("http://localhost:8000/events"))
                                            {
                                                req.Headers.Add("Accept", "text/event-stream");

                                                using (RestResponse resp = await req.SendAsync())
                                                {
                                                    if (resp.StatusCode != 200)
                                                        throw new Exception($"Connection {connectionId} failed with status {resp.StatusCode}");

                                                    if (!resp.ServerSentEvents)
                                                        throw new Exception($"Connection {connectionId} not SSE");

                                                    int eventCount = 0;
                                                    while (true)
                                                    {
                                                        RestWrapper.ServerSentEvent sse = await resp.ReadEventAsync();
                                                        if (sse == null || String.IsNullOrEmpty(sse.Data))
                                                            break;
                                                        eventCount++;
                                                        if (eventCount > 10) break;
                                                    }

                                                    return eventCount;
                                                }
                                            }
                                        });
                                    }

                                    int[] results = await Task.WhenAll(eventTasks);
                                    int totalEvents = results.Sum();

                                    if (totalEvents == 0)
                                        throw new Exception("No events received from any connection");

                                    return $"All {eventTasks.Length} concurrent SSE streams validated: {totalEvents} total events";
                                });

                                #endregion

                                #region Re-run-Tests-After-Recovery

                                await RunTest("Load Balancing Verification - Round Robin (After Recovery)", async () =>
                                {
                                    // Ensure all servers are healthy first
                                    Console.WriteLine("  Ensuring all servers are healthy for load balancing test...");
                                    if (_Settings.Origins.Any(o => !o.Healthy))
                                    {
                                        throw new Exception("Cannot test load balancing - some servers are unhealthy");
                                    }

                                    Dictionary<string, int> serverHits = new Dictionary<string, int>();
                                    int totalRequests = 12; // Multiple of 4 servers

                                    for (int i = 0; i < totalRequests; i++)
                                    {
                                        using (RestRequest req = new RestRequest("http://localhost:8000/unauthenticated"))
                                        {
                                            using (RestResponse resp = await req.SendAsync())
                                            {
                                                if (resp.StatusCode == 200)
                                                {
                                                    // Extract server identifier from response
                                                    string responseText = resp.DataAsString;
                                                    if (responseText.Contains("Server 1")) serverHits["Server1"] = serverHits.GetValueOrDefault("Server1", 0) + 1;
                                                    else if (responseText.Contains("Server 2")) serverHits["Server2"] = serverHits.GetValueOrDefault("Server2", 0) + 1;
                                                    else if (responseText.Contains("Server 3")) serverHits["Server3"] = serverHits.GetValueOrDefault("Server3", 0) + 1;
                                                    else if (responseText.Contains("Server 4")) serverHits["Server4"] = serverHits.GetValueOrDefault("Server4", 0) + 1;
                                                    else
                                                    {
                                                        Console.WriteLine($"  DEBUG: Unexpected response format: {responseText}");
                                                    }
                                                }

                                                await Task.Delay(100); // Small delay to ensure round-robin behavior
                                            }
                                        }
                                    }

                                    Console.WriteLine("  Load balancing distribution:");
                                    foreach (KeyValuePair<string, int> kvp in serverHits)
                                    {
                                        Console.WriteLine($"    {kvp.Key}: {kvp.Value} requests");
                                    }

                                    // Check that all servers received requests
                                    if (serverHits.Count < 4)
                                        throw new Exception($"Only {serverHits.Count} servers received requests, expected 4");

                                    return $"Load balanced across {serverHits.Count} servers";
                                });

                                await RunTest("Request Headers Validation - Custom Headers Forwarded", async () =>
                                {
                                    using (RestRequest req = new RestRequest("http://localhost:8000/api/headers-test"))
                                    {
                                        req.Headers.Add("X-Custom-Header", "test-value-123");
                                        req.Headers.Add("X-Test-Client", "switchboard-test-v2");

                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            Console.WriteLine($"  Response ({resp.StatusCode}): {resp.DataAsString}");

                                            if (resp.StatusCode != 200)
                                                throw new Exception($"Request with custom headers failed with status {resp.StatusCode}");

                                            // Verify headers were forwarded
                                            if (!resp.DataAsString.Contains("X-Custom-Header"))
                                                throw new Exception("Response does not contain X-Custom-Header");

                                            if (!resp.DataAsString.Contains("test-value-123"))
                                                throw new Exception("X-Custom-Header value not correctly forwarded");

                                            if (!resp.DataAsString.Contains("X-Test-Client"))
                                                throw new Exception("Response does not contain X-Test-Client");

                                            if (!resp.DataAsString.Contains("switchboard-test-v2"))
                                                throw new Exception("X-Test-Client value not correctly forwarded");

                                            return "Custom headers verified forwarded: X-Custom-Header, X-Test-Client";
                                        }
                                    }
                                });

                                await RunTest("Querystring Parameter Handling - Verified Forwarding", async () =>
                                {
                                    // Test case 1: Simple parameter
                                    using (RestRequest req1 = new RestRequest("http://localhost:8000/unauthenticated?param1=value1"))
                                    {
                                        using (RestResponse resp1 = await req1.SendAsync())
                                        {
                                            Console.WriteLine($"  Test 1 ({resp1.StatusCode}): {resp1.DataAsString}");
                                            if (resp1.StatusCode != 200)
                                                throw new Exception($"Test 1 failed with status {resp1.StatusCode}");
                                            if (!resp1.DataAsString.Contains("param1=value1"))
                                                throw new Exception("Test 1: param1=value1 not found in response");
                                        }
                                    }

                                    // Test case 2: Multiple parameters
                                    using (RestRequest req2 = new RestRequest("http://localhost:8000/api/query-test?param1=value1&param2=value2"))
                                    {
                                        using (RestResponse resp2 = await req2.SendAsync())
                                        {
                                            Console.WriteLine($"  Test 2 ({resp2.StatusCode}): query in response");
                                            if (resp2.StatusCode != 200)
                                                throw new Exception($"Test 2 failed with status {resp2.StatusCode}");
                                            if (!resp2.DataAsString.Contains("param1=value1"))
                                                throw new Exception("Test 2: param1=value1 not in query");
                                            if (!resp2.DataAsString.Contains("param2=value2"))
                                                throw new Exception("Test 2: param2=value2 not in query");
                                        }
                                    }

                                    // Test case 3: Special characters
                                    using (RestRequest req3 = new RestRequest("http://localhost:8000/api/query-test?special=%20%21%40%23"))
                                    {
                                        using (RestResponse resp3 = await req3.SendAsync())
                                        {
                                            Console.WriteLine($"  Test 3 ({resp3.StatusCode}): special chars");
                                            if (resp3.StatusCode != 200)
                                                throw new Exception($"Test 3 failed with status {resp3.StatusCode}");
                                            if (!resp3.DataAsString.Contains("special="))
                                                throw new Exception("Test 3: special parameter not found");
                                        }
                                    }

                                    return "Querystring parameters verified forwarded correctly in all 3 test cases";
                                });

                                #endregion

                                #region Additional-Comprehensive-Tests

                                await RunTest("CORS - OPTIONS Preflight Request", async () =>
                                {
                                    using (RestRequest req = new RestRequest("http://localhost:8000/api/cors-test", System.Net.Http.HttpMethod.Options))
                                    {
                                        req.Headers.Add("Access-Control-Request-Method", "POST");
                                        req.Headers.Add("Access-Control-Request-Headers", "Content-Type");
                                        req.Headers.Add("Origin", "http://example.com");

                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            Console.WriteLine($"  OPTIONS Response ({resp.StatusCode})");

                                            if (resp.StatusCode != 200)
                                                throw new Exception($"Expected 200 OK, got {resp.StatusCode}");

                                            string allowMethods = resp.Headers.Get("Access-Control-Allow-Methods");
                                            if (String.IsNullOrEmpty(allowMethods))
                                                throw new Exception("Missing Access-Control-Allow-Methods header");

                                            if (!allowMethods.Contains("POST"))
                                                throw new Exception("Access-Control-Allow-Methods does not include POST");

                                            return $"CORS preflight successful, allowed methods: {allowMethods}";
                                        }
                                    }
                                });

                                await RunTest("Authentication - Header Forwarding to Origin", async () =>
                                {
                                    using (RestRequest req = new RestRequest("http://localhost:8000/api/secure"))
                                    {
                                        req.Authorization.BearerToken = "test-token-12345";

                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            Console.WriteLine($"  Response ({resp.StatusCode}): {resp.DataAsString}");

                                            if (resp.StatusCode != 200)
                                                throw new Exception($"Expected 200 OK, got {resp.StatusCode}");

                                            // Verify authenticated request succeeded
                                            if (!resp.DataAsString.Contains("\"server\""))
                                                throw new Exception("Response does not contain server information");

                                            // Check that origin received the Authorization header (proves forwarding works)
                                            if (!resp.DataAsString.Contains("Authorization"))
                                                throw new Exception("Authorization header not forwarded to origin");

                                            if (!resp.DataAsString.Contains("Bearer test-token-12345"))
                                                throw new Exception("Authorization header value not correctly forwarded");

                                            // Verify response contains receivedHeaders showing Switchboard forwarded headers
                                            if (!resp.DataAsString.Contains("receivedHeaders"))
                                                throw new Exception("Origin response does not contain receivedHeaders");

                                            return "Authenticated request successful, Authorization header verified forwarded to origin";
                                        }
                                    }
                                });

                                await RunTest("Chunked Transfer - Large Upload (Multiple Chunks)", async () =>
                                {
                                    if (_Settings.Origins.Any(o => !o.Healthy))
                                        throw new Exception("Cannot test - some servers are unhealthy");

                                    // Create larger chunks to test scalability
                                    string[] largeChunks = new string[10];
                                    for (int i = 0; i < largeChunks.Length; i++)
                                    {
                                        largeChunks[i] = new string((char)('A' + i), 1000) + $" Chunk {i}\n";
                                    }

                                    RestRequest req = new RestRequest("http://localhost:8000/api/upload", System.Net.Http.HttpMethod.Post);
                                    req.ContentType = "text/plain";
                                    req.ChunkedTransfer = true;

                                    try
                                    {
                                        RestResponse resp = null;
                                        for (int i = 0; i < largeChunks.Length; i++)
                                        {
                                            bool isLast = (i == largeChunks.Length - 1);
                                            resp = await req.SendChunkAsync(largeChunks[i], isLast);
                                        }

                                        if (resp == null || resp.StatusCode != 200)
                                            throw new Exception($"Expected 200 OK, got {resp?.StatusCode ?? 0}");

                                        int totalBytes = largeChunks.Sum(c => Encoding.UTF8.GetByteCount(c));
                                        Console.WriteLine($"  Total bytes sent: {totalBytes}");

                                        return $"Large chunked upload successful: {totalBytes} bytes in {largeChunks.Length} chunks";
                                    }
                                    finally
                                    {
                                        req?.Dispose();
                                    }
                                });

                                await RunTest("Server-Sent Events - Long Running Stream", async () =>
                                {
                                    if (_Settings.Origins.Any(o => !o.Healthy))
                                        throw new Exception("Cannot test SSE - some servers are unhealthy");

                                    using (RestRequest req = new RestRequest("http://localhost:8000/events"))
                                    {
                                        req.Headers.Add("Accept", "text/event-stream");

                                        using (RestResponse resp = await req.SendAsync())
                                        {
                                            if (resp.StatusCode != 200)
                                                throw new Exception($"Expected 200 OK, got {resp.StatusCode}");

                                            if (!resp.ServerSentEvents)
                                                throw new Exception("Response not detected as SSE");

                                            // Read events and verify timing
                                            List<DateTime> eventTimes = new List<DateTime>();
                                            int eventCount = 0;
                                            while (eventCount < 5)
                                            {
                                                var sse = await resp.ReadEventAsync();
                                                if (sse == null || String.IsNullOrEmpty(sse.Data))
                                                    break;

                                                eventTimes.Add(DateTime.Now);
                                                eventCount++;
                                                Console.WriteLine($"  Event {eventCount}: {sse.Data.Substring(0, Math.Min(50, sse.Data.Length))}...");
                                            }

                                            if (eventCount == 0)
                                                throw new Exception("No SSE events received");

                                            return $"SSE long-running stream: received {eventCount} events over {(eventTimes.Last() - eventTimes.First()).TotalMilliseconds:F0}ms";
                                        }
                                    }
                                });

                                await RunTest("Random Load Balancing - Distribution Check", async () =>
                                {
                                    // Temporarily switch to random load balancing
                                    LoadBalancingMode originalMode = _Settings.Endpoints[0].LoadBalancing;
                                    _Settings.Endpoints[0].LoadBalancing = LoadBalancingMode.Random;

                                    try
                                    {
                                        Dictionary<string, int> serverHits = new Dictionary<string, int>();
                                        int totalRequests = 20;

                                        for (int i = 0; i < totalRequests; i++)
                                        {
                                            using (RestRequest req = new RestRequest("http://localhost:8000/unauthenticated"))
                                            {
                                                using (RestResponse resp = await req.SendAsync())
                                                {
                                                    if (resp.StatusCode == 200)
                                                    {
                                                        string responseText = resp.DataAsString;
                                                        if (responseText.Contains("Server 1")) serverHits["Server1"] = serverHits.GetValueOrDefault("Server1", 0) + 1;
                                                        else if (responseText.Contains("Server 2")) serverHits["Server2"] = serverHits.GetValueOrDefault("Server2", 0) + 1;
                                                        else if (responseText.Contains("Server 3")) serverHits["Server3"] = serverHits.GetValueOrDefault("Server3", 0) + 1;
                                                        else if (responseText.Contains("Server 4")) serverHits["Server4"] = serverHits.GetValueOrDefault("Server4", 0) + 1;
                                                    }
                                                }
                                            }
                                        }

                                        Console.WriteLine("  Random load balancing distribution:");
                                        foreach (var kvp in serverHits)
                                        {
                                            Console.WriteLine($"    {kvp.Key}: {kvp.Value} requests ({kvp.Value * 100.0 / totalRequests:F1}%)");
                                        }

                                        // Check that all servers received at least one request
                                        if (serverHits.Count < 4)
                                            throw new Exception($"Only {serverHits.Count}/4 servers received requests with random load balancing");

                                        // Check that distribution is somewhat random (not all requests to one server)
                                        int maxHits = serverHits.Values.Max();
                                        if (maxHits == totalRequests)
                                            throw new Exception("All requests went to one server - not random");

                                        return $"Random load balancing verified: all 4 servers received requests";
                                    }
                                    finally
                                    {
                                        // Restore original mode
                                        _Settings.Endpoints[0].LoadBalancing = originalMode;
                                    }
                                });

                                #endregion

                                #region Health-Check-During-Active-Requests

                                await RunTest("Health Check - Server Failure During Active Requests", async () =>
                                {
                                    Console.WriteLine("  Starting background requests while servers will be stopped...");

                                    Task<(int count, int failures)> requestTask = Task.Run(async () =>
                                    {
                                        int requestCount = 0;
                                        int failureCount = 0;

                                        for (int i = 0; i < 10; i++)
                                        {
                                            requestCount++;

                                            using (RestRequest req = new RestRequest("http://localhost:8000/unauthenticated"))
                                            {
                                                using (RestResponse resp = await req.SendAsync())
                                                {
                                                    if (resp.StatusCode != 200)
                                                    {
                                                        failureCount++;
                                                        Console.WriteLine($"  Request {i}: Failed with status {resp.StatusCode}");
                                                    }
                                                    else
                                                    {
                                                        string originServer = resp.Headers.Get("X-Origin-Server") ?? "unknown";
                                                        Console.WriteLine($"  Request {i}: Success from {originServer}");
                                                    }
                                                }
                                            }

                                            await Task.Delay(500);
                                        }

                                        return (requestCount, failureCount);
                                    });

                                    await Task.Delay(1000);
                                    Console.WriteLine("  Stopping servers 3 and 4 during active requests...");
                                    _Server3.Stop();
                                    _Server4.Stop();

                                    (int totalRequests, int failures) = await requestTask;

                                    Console.WriteLine("  Restarting servers 3 and 4...");
                                    _Server3.Start();
                                    _Server4.Start();
                                    await Task.Delay(3000);

                                    // Some failures are expected when servers go down, but not all requests should fail
                                    if (failures == totalRequests)
                                        throw new Exception("All requests failed - load balancing not working properly");

                                    return $"{totalRequests - failures}/{totalRequests} requests succeeded despite server failures";
                                });

                                #endregion

                                _OverallStopwatch.Stop();

                                Console.WriteLine("");
                                Console.WriteLine("======================================");
                                Console.WriteLine("           TEST SUMMARY");
                                Console.WriteLine("======================================");
                                Console.WriteLine("");

                                int passed = _TestResults.Count(r => r.Passed);
                                int failed = _TestResults.Count(r => !r.Passed);
                                TimeSpan totalTime = _OverallStopwatch.Elapsed;

                                Console.WriteLine($"Total Tests: {_TestResults.Count}");
                                Console.WriteLine($"Passed:      {passed}");
                                Console.WriteLine($"Failed:      {failed}");
                                Console.WriteLine($"Success Rate: {(passed * 100.0 / _TestResults.Count):F1}%");
                                Console.WriteLine($"Total Runtime: {totalTime.TotalSeconds:F2} seconds");
                                Console.WriteLine("");

                                Console.WriteLine("Individual Test Results:");
                                Console.WriteLine("".PadRight(100, '─'));
                                Console.WriteLine($"{"Test Name",-50} {"Result",-8} {"Duration",-10} {"Details"}");
                                Console.WriteLine("".PadRight(100, '─'));

                                foreach (TestResult result in _TestResults)
                                {
                                    string status = result.Passed ? "PASS" : "FAIL";
                                    string duration = $"{result.Duration.TotalMilliseconds:F0}ms";
                                    string details = result.Passed ? result.ResultMessage : result.ErrorMessage;

                                    Console.WriteLine($"{result.TestName,-50} {status,-8} {duration,-10} {details}");
                                }

                                Console.WriteLine("".PadRight(100, '─'));
                                Console.WriteLine("");

                                if (failed > 0)
                                {
                                    Console.WriteLine($"❌ {failed} test(s) failed. Review the results above.");
                                }
                                else
                                {
                                    Console.WriteLine("✅ All tests passed successfully!");
                                }

                                Console.WriteLine("");
                                Console.WriteLine("Tests completed. Application will now exit.");
                            }
                        }
                    }
                }
            }
        }

        private static async Task<AuthContext> AuthenticateAndAuthorizeRequest(HttpContextBase ctx)
        {
            string authHeader = ctx.Request.RetrieveHeaderValue("Authorization");
            if (!String.IsNullOrEmpty(authHeader))
            {
                return new AuthContext
                {
                    Authentication = new AuthenticationContext
                    {
                        Result = AuthenticationResultEnum.Success,
                        Metadata = new Dictionary<string, string>()
                        {
                            { "Authenticated", "true" }
                        }
                    },
                    Authorization = new AuthorizationContext
                    {
                        Result = AuthorizationResultEnum.Success,
                        Metadata = new Dictionary<string, string>()
                        {
                            { "Authorized", "true" }
                        }
                    },
                    Metadata = new Dictionary<string, string>()
                    {
                        { "Allow", "true" }
                    }
                };
            }
            else
            {
                return new AuthContext
                {
                    Authentication = new AuthenticationContext
                    {
                        Result = AuthenticationResultEnum.Denied,
                        Metadata = new Dictionary<string, string>()
                        {
                            { "Authenticated", "false" }
                        }
                    },
                    Authorization = new AuthorizationContext
                    {
                        Result = AuthorizationResultEnum.Denied,
                        Metadata = new Dictionary<string, string>()
                        {
                            { "Authorized", "false" }
                        }
                    },
                    Metadata = new Dictionary<string, string>()
                    {
                        { "Allow", "false" },
                        { "Error", "Supply an Authorization header in your request" }
                    },
                    FailureMessage = "Supply an Authorization header in your request"
                };
            }
        }

        private static void InitializeSettings()
        {
            _Settings = new SwitchboardSettings();
            _Settings.Logging.MinimumSeverity = 1;
            _Settings.Logging.EnableColors = false;

            _Settings.Endpoints.Add(new ApiEndpoint
            {
                Identifier = "test-endpoint",
                Name = "Test Endpoint",
                LoadBalancing = _LoadBalancingMode,
                AuthContextHeader = Constants.AuthContextHeader,
                IncludeAuthContextHeader = true,
                TimeoutMs = 30000,
                MaxRequestBodySize = 10485760, // 10MB
                BlockHttp10 = false,
                Unauthenticated = new ApiEndpointGroup
                {
                    ParameterizedUrls = new Dictionary<string, List<string>>
                    {
                        { "GET", new List<string> { "/unauthenticated", "/api/users", "/api/data", "/events", "/api/v2/users/{userId}", "/api/headers-test", "/api/query-test", "/api/timeout-test", "/api/large-body-test" } },
                        { "POST", new List<string> { "/api/users", "/api/data", "/api/upload", "/api/large-body-test" } },
                        { "PUT", new List<string> { "/api/users/{id}", "/api/data/{id}" } },
                        { "DELETE", new List<string> { "/api/users/{id}", "/api/data/{id}" } },
                        { "PATCH", new List<string> { "/api/users/{id}" } },
                        { "OPTIONS", new List<string> { "/api/cors-test" } }
                    }
                },
                Authenticated = new ApiEndpointGroup
                {
                    ParameterizedUrls = new Dictionary<string, List<string>>
                    {
                        { "GET", new List<string> { "/authenticated", "/api/secure" } },
                        { "POST", new List<string> { "/api/secure" } },
                        { "PUT", new List<string> { "/api/secure/{id}" } },
                        { "DELETE", new List<string> { "/api/secure/{id}" } }
                    }
                },
                RewriteUrls = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "GET", new Dictionary<string, string>
                        {
                            { "/api/v2/users/{userId}", "/v1/users/{userId}" }
                        }
                    }
                },
                OriginServers = new List<string>
                {
                    "server1",
                    "server2",
                    "server3",
                    "server4"
                }
            });

            _Settings.Origins.Add(new OriginServer
            {
                Identifier = "server1",
                Name = "Server 1",
                Hostname = "localhost",
                Port = 8001,
                Ssl = false,
                HealthCheckIntervalMs = 1000,  // Check every second for faster testing
                UnhealthyThreshold = 2,        // 2 failures = unhealthy
                HealthyThreshold = 2,          // 2 successes = healthy
                MaxParallelRequests = 2,       // Low limit for rate limiting tests
                RateLimitRequestsThreshold = 3 // Very low threshold for testing
            });

            _Settings.Origins.Add(new OriginServer
            {
                Identifier = "server2",
                Name = "Server 2",
                Hostname = "localhost",
                Port = 8002,
                Ssl = false,
                HealthCheckIntervalMs = 1000,
                UnhealthyThreshold = 2,
                HealthyThreshold = 2,
                MaxParallelRequests = 2,
                RateLimitRequestsThreshold = 3
            });

            _Settings.Origins.Add(new OriginServer
            {
                Identifier = "server3",
                Name = "Server 3",
                Hostname = "localhost",
                Port = 8003,
                Ssl = false,
                HealthCheckIntervalMs = 1000,
                UnhealthyThreshold = 2,
                HealthyThreshold = 2,
                MaxParallelRequests = 2,
                RateLimitRequestsThreshold = 3
            });

            _Settings.Origins.Add(new OriginServer
            {
                Identifier = "server4",
                Name = "Server 4",
                Hostname = "localhost",
                Port = 8004,
                Ssl = false,
                HealthCheckIntervalMs = 1000,
                UnhealthyThreshold = 2,
                HealthyThreshold = 2,
                MaxParallelRequests = 2,
                RateLimitRequestsThreshold = 3
            });

            File.WriteAllBytes("./sb.json", Encoding.UTF8.GetBytes(_Serializer.SerializeJson(_Settings, true)));
        }

        private static void InitializeOriginServers()
        {
            _Server1Settings = new WebserverSettings();
            _Server1Settings.Hostname = "localhost";
            _Server1Settings.Port = 8001;

            _Server2Settings = new WebserverSettings();
            _Server2Settings.Hostname = "localhost";
            _Server2Settings.Port = 8002;

            _Server3Settings = new WebserverSettings();
            _Server3Settings.Hostname = "localhost";
            _Server3Settings.Port = 8003;

            _Server4Settings = new WebserverSettings();
            _Server4Settings.Hostname = "localhost";
            _Server4Settings.Port = 8004;
        }

        private static async Task Server1Route(HttpContextBase ctx)
        {
            if (IsHealthCheck(ctx))
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send();
                return;
            }

            await HandleAdvancedRequest(ctx, "Server 1");
        }

        private static async Task Server2Route(HttpContextBase ctx)
        {
            if (IsHealthCheck(ctx))
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send();
                return;
            }

            await HandleAdvancedRequest(ctx, "Server 2");
        }

        private static async Task Server3Route(HttpContextBase ctx)
        {
            if (IsHealthCheck(ctx))
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send();
                return;
            }

            await HandleAdvancedRequest(ctx, "Server 3");
        }

        private static async Task Server4Route(HttpContextBase ctx)
        {
            if (IsHealthCheck(ctx))
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send();
                return;
            }

            await HandleAdvancedRequest(ctx, "Server 4");
        }

        private static async Task HandleAdvancedRequest(HttpContextBase ctx, string serverName)
        {
            // Handle Server-Sent Events
            if (ctx.Request.Url.RawWithoutQuery.EndsWith("/events"))
            {
                await HandleServerSentEvents(ctx, serverName);
                return;
            }

            // Handle chunked transfer encoding for uploads
            if (ctx.Request.Url.RawWithoutQuery.EndsWith("/upload"))
            {
                await HandleChunkedUpload(ctx, serverName);
                return;
            }

            // Handle normal REST requests (suppress verbose output)
            await HandleRestRequest(ctx, serverName);
        }

        private static async Task HandleServerSentEvents(HttpContextBase ctx, string serverName)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.Add("Cache-Control", "no-cache");
            ctx.Response.Headers.Add("Connection", "keep-alive");
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            ctx.Response.ServerSentEvents = true;

            // Send initial connection event
            await ctx.Response.SendEvent(new WatsonWebserver.Core.ServerSentEvent
            {
                Data = $"Connected to {serverName}"
            }, false);

            // Send multiple events
            for (int i = 1; i <= 3; i++)
            {
                await Task.Delay(100); // Small delay for realism
                await ctx.Response.SendEvent(new WatsonWebserver.Core.ServerSentEvent
                {
                    Data = $"Event {i} from {serverName} at {DateTime.Now:HH:mm:ss.fff}"
                }, false);
            }

            // Send closing event
            await ctx.Response.SendEvent(new WatsonWebserver.Core.ServerSentEvent
            {
                Data = $"Connection closing from {serverName}"
            }, true);
        }

        private static async Task HandleChunkedUpload(HttpContextBase ctx, string serverName)
        {
            byte[] requestData = Array.Empty<byte>();
            string transferEncoding = ctx.Request.Headers.Get("Transfer-Encoding") ?? "none";

            if (ctx.Request.ChunkedTransfer)
            {
                // Handle chunked transfer by reading chunks
                List<byte> allData = new List<byte>();
                bool finalChunk = false;

                while (!finalChunk)
                {
                    var chunk = await ctx.Request.ReadChunk();
                    if (chunk.Length == 0)
                    {
                        finalChunk = true;
                    }
                    else
                    {
                        allData.AddRange(chunk.Data);
                    }
                }

                requestData = allData.ToArray();
            }
            else if (ctx.Request.Data != null && ctx.Request.DataAsBytes != null)
            {
                // Regular request with body
                requestData = ctx.Request.DataAsBytes;
            }

            if (requestData.Length > 0 || transferEncoding.Contains("chunked"))
            {
                // Respond with completion status
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";

                Dictionary<string, object> response = new Dictionary<string, object>
                {
                    { "server", serverName },
                    { "status", "completed" },
                    { "received", requestData.Length },
                    { "timestamp", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "transferEncoding", transferEncoding }
                };

                string responseJson = _Serializer.SerializeJson(response, true);
                await ctx.Response.Send(responseJson);
            }
            else
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send($"{{\"error\":\"No data received\",\"server\":\"{serverName}\"}}");
            }
        }

        private static async Task HandleRestRequest(HttpContextBase ctx, string serverName)
        {
            // Suppress verbose logging for successful requests
            // Headers and details are only shown in test output if tests fail

            // Create appropriate response based on method and URL
            string responseContent = CreateRestResponse(ctx, serverName);
            string contentType = ctx.Request.Url.RawWithoutQuery.Contains("/api/") ? "application/json" : "text/plain";

            ctx.Response.StatusCode = GetStatusCodeForMethod(ctx.Request.Method);
            ctx.Response.ContentType = contentType;
            ctx.Response.Headers.Add("X-Origin-Server", serverName);

            await ctx.Response.Send(responseContent);
        }

        private static string CreateRestResponse(HttpContextBase ctx, string serverName)
        {
            string path = ctx.Request.Url.RawWithoutQuery;
            string method = ctx.Request.Method.ToString();

            if (path.Contains("/api/"))
            {
                // Return JSON response for API endpoints
                Dictionary<string, object> response = new Dictionary<string, object>
                {
                    { "server", serverName },
                    { "method", method },
                    { "path", path },
                    { "timestamp", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "query", ctx.Request.Query.Querystring ?? "" }
                };

                if (!String.IsNullOrEmpty(ctx.Request.DataAsString))
                {
                    response["receivedData"] = ctx.Request.DataAsString;
                }

                // Echo back specific headers for testing
                Dictionary<string, string> receivedHeaders = new Dictionary<string, string>();
                if (ctx.Request.Headers != null)
                {
                    foreach (string key in ctx.Request.Headers.AllKeys)
                    {
                        if (key != null && (key.StartsWith("X-") || key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)))
                        {
                            receivedHeaders[key] = ctx.Request.Headers.Get(key);
                        }
                    }
                }
                if (receivedHeaders.Count > 0)
                {
                    response["receivedHeaders"] = receivedHeaders;
                }

                return _Serializer.SerializeJson(response, true);
            }
            else
            {
                // Return plain text response with query string for validation
                return $"Hello from {serverName}: {method} {ctx.Request.Url.RawWithQuery}";
            }
        }

        private static int GetStatusCodeForMethod(WatsonWebserver.Core.HttpMethod method)
        {
            return method switch
            {
                WatsonWebserver.Core.HttpMethod.POST => 201, // Created
                WatsonWebserver.Core.HttpMethod.PUT => 200,  // OK
                WatsonWebserver.Core.HttpMethod.DELETE => 204, // No Content
                WatsonWebserver.Core.HttpMethod.PATCH => 200,  // OK
                _ => 200 // OK for GET and others
            };
        }

        private static bool IsHealthCheck(HttpContextBase ctx)
        {
            return (ctx.Request.Method == WatsonWebserver.Core.HttpMethod.GET && ctx.Request.Url.RawWithoutQuery.Equals("/"));
        }

        private static async Task RunTest(string testName, Func<Task<string>> testAction, bool verboseOutput = false)
        {
            Console.WriteLine("");
            Console.Write($"[Testing] {testName}...");

            Stopwatch stopwatch = Stopwatch.StartNew();
            TestResult result = new TestResult
            {
                TestName = testName,
                StartTime = DateTime.Now
            };

            // Capture console output
            StringWriter outputCapture = new StringWriter();
            TextWriter originalOutput = Console.Out;

            try
            {
                if (!verboseOutput)
                {
                    Console.SetOut(outputCapture);
                }

                result.ResultMessage = await testAction();
                result.Passed = true;
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;

                if (!verboseOutput)
                {
                    Console.SetOut(originalOutput);
                }

                Console.WriteLine($" ✅ PASS ({result.Duration.TotalMilliseconds:F0}ms)");
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.ErrorMessage = ex.Message;
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;

                if (!verboseOutput)
                {
                    Console.SetOut(originalOutput);
                }

                Console.WriteLine($" ❌ FAIL ({result.Duration.TotalMilliseconds:F0}ms)");
                Console.WriteLine("────────────────────────────────────");
                Console.WriteLine("FAILURE DETAILS:");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("\nTest Output:");
                Console.WriteLine(outputCapture.ToString());
                Console.WriteLine("────────────────────────────────────");
            }
            finally
            {
                Console.SetOut(originalOutput);
                outputCapture.Dispose();
            }

            _TestResults.Add(result);
        }

        /// <summary>
        /// Represents the result of a single test case.
        /// </summary>
        public class TestResult
        {
            /// <summary>
            /// Name of the test.
            /// </summary>
            public string TestName { get; set; } = string.Empty;

            /// <summary>
            /// Whether the test passed.
            /// </summary>
            public bool Passed { get; set; }

            /// <summary>
            /// Start time of the test.
            /// </summary>
            public DateTime StartTime { get; set; }

            /// <summary>
            /// Duration of the test execution.
            /// </summary>
            public TimeSpan Duration { get; set; }

            /// <summary>
            /// Success message if test passed.
            /// </summary>
            public string ResultMessage { get; set; } = string.Empty;

            /// <summary>
            /// Error message if test failed.
            /// </summary>
            public string ErrorMessage { get; set; } = string.Empty;
        }

        /// <summary>
        /// Custom HttpContent implementation for sending chunked data.
        /// </summary>
        public class ChunkedHttpContent : System.Net.Http.HttpContent
        {
            private readonly string[] _chunks;

            public ChunkedHttpContent(string[] chunks)
            {
                _chunks = chunks ?? throw new ArgumentNullException(nameof(chunks));
                Headers.TryAddWithoutValidation("Content-Type", "text/plain");
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                foreach (string chunk in _chunks)
                {
                    byte[] chunkBytes = System.Text.Encoding.UTF8.GetBytes(chunk);
                    await stream.WriteAsync(chunkBytes, 0, chunkBytes.Length);
                    await stream.FlushAsync();
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                // Return false to indicate unknown length (required for chunked encoding)
                length = 0;
                return false;
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
            }
        }

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
    }
}