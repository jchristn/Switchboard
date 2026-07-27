namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;

    using RestWrapper;
    using Test.Shared.Harness;
    using Touchstone.Core;

    /// <summary>
    /// Integration suites for streaming behavior: chunked transfer encoding (upload and download)
    /// and Server-Sent Events, exercised through both RestWrapper and <see cref="HttpClient"/>.
    /// </summary>
    public static class StreamingSuites
    {
        /// <summary>
        /// All streaming integration suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    ChunkedTransferSuite(),
                    ServerSentEventsSuite()
                };
            }
        }

        /// <summary>
        /// Chunked transfer encoding across the proxy.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ChunkedTransferSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Chunked",
                displayName: "Chunked Transfer",
                beforeSuiteAsync: IntegrationSupport.EnsureSharedStarted,
                cases: new List<TestCaseDescriptor>
                {
                    IntegrationSupport.SharedCase("Chunked", "ClientUpload", "Client-side chunked upload is forwarded and byte count matches", async (h, ct) =>
                    {
                        string[] chunks =
                        {
                            "This is the first chunk of data for testing chunked transfer encoding.\n",
                            "Here comes the second chunk with more test data to verify the chunked upload.\n",
                            "Third chunk containing additional content for a comprehensive test.\n",
                            "Final chunk to complete the chunked transfer encoding test.\n"
                        };

                        RestRequest req = new RestRequest(h.Url("/api/upload"), HttpMethod.Post);
                        req.ContentType = "text/plain";
                        req.ChunkedTransfer = true;
                        try
                        {
                            RestResponse? resp = null;
                            for (int i = 0; i < chunks.Length; i++)
                                resp = await req.SendChunkAsync(Encoding.UTF8.GetBytes(chunks[i]), i == chunks.Length - 1);

                            Check.True(resp != null, "response received");
                            Check.Equal(200, resp!.StatusCode, "upload status");
                            Check.Contains(resp.DataAsString, "completed", "completed marker");

                            int totalBytes = chunks.Sum(c => Encoding.UTF8.GetByteCount(c));
                            string compact = Compact(resp.DataAsString);
                            Check.Contains(compact, "\"received\":" + totalBytes, "received byte count");
                        }
                        finally
                        {
                            req.Dispose();
                        }
                    }),

                    IntegrationSupport.SharedCase("Chunked", "LargeUpload", "Large multi-chunk upload succeeds", async (h, ct) =>
                    {
                        string[] chunks = new string[10];
                        for (int i = 0; i < chunks.Length; i++)
                            chunks[i] = new string((char)('A' + i), 1000) + " Chunk " + i + "\n";

                        RestRequest req = new RestRequest(h.Url("/api/upload"), HttpMethod.Post);
                        req.ContentType = "text/plain";
                        req.ChunkedTransfer = true;
                        try
                        {
                            RestResponse? resp = null;
                            for (int i = 0; i < chunks.Length; i++)
                                resp = await req.SendChunkAsync(Encoding.UTF8.GetBytes(chunks[i]), i == chunks.Length - 1);

                            Check.True(resp != null && resp.StatusCode == 200, "large upload status");
                            int totalBytes = chunks.Sum(c => Encoding.UTF8.GetByteCount(c));
                            Check.Contains(Compact(resp!.DataAsString), "\"received\":" + totalBytes, "large received byte count");
                        }
                        finally
                        {
                            req.Dispose();
                        }
                    }),

                    IntegrationSupport.SharedCase("Chunked", "RegularUploadResponse", "Regular upload receives completion response", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/api/upload"), HttpMethod.Post))
                        {
                            req.ContentType = "application/json";
                            using (RestResponse resp = await req.SendAsync("{\"test\":\"chunked response\"}"))
                            {
                                Check.Equal(200, resp.StatusCode, "regular upload status");
                                Check.Contains(resp.DataAsString, "completed", "completed marker");
                                Check.Contains(resp.DataAsString, "received", "received marker");
                            }
                        }
                    }),

                    IntegrationSupport.SharedCase("Chunked", "DownloadRestWrapper", "Chunked response download via RestWrapper", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/chunked-download")))
                        using (RestResponse resp = await req.SendAsync())
                        {
                            Check.Equal(200, resp.StatusCode, "download status");
                            Check.True(resp.ChunkedTransferEncoding, "chunked encoding flagged");

                            List<string> received = new List<string>();
                            while (true)
                            {
                                ChunkData chunk = await resp.ReadChunkAsync();
                                if (chunk == null || chunk.Data == null || chunk.Data.Length == 0) break;
                                received.Add(Encoding.UTF8.GetString(chunk.Data));
                            }

                            Check.Equal(OriginHost.DownloadChunks.Length, received.Count, "chunk count");
                            for (int i = 0; i < received.Count; i++)
                                Check.Equal(OriginHost.DownloadChunks[i].TrimEnd('\r', '\n'), received[i].TrimEnd('\r', '\n'), "chunk " + i);
                        }
                    }),

                    IntegrationSupport.SharedCase("Chunked", "DownloadHttpClient", "Chunked response download via HttpClient", async (h, ct) =>
                    {
                        using (HttpClient client = new HttpClient())
                        using (HttpResponseMessage resp = await client.GetAsync(h.Url("/chunked-download"), HttpCompletionOption.ResponseHeadersRead, ct))
                        {
                            Check.True(resp.IsSuccessStatusCode, "download success");
                            Check.True(resp.Headers.TransferEncodingChunked == true, "transfer-encoding chunked");

                            string body = await resp.Content.ReadAsStringAsync(ct);
                            foreach (string expected in OriginHost.DownloadChunks)
                                Check.Contains(body, expected.TrimEnd('\r', '\n'), "body contains chunk text");
                        }
                    })
                });
        }

        private static string Compact(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
            return value.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");
        }

        /// <summary>
        /// Server-Sent Events across the proxy.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ServerSentEventsSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Sse",
                displayName: "Server-Sent Events",
                beforeSuiteAsync: IntegrationSupport.EnsureSharedStarted,
                cases: new List<TestCaseDescriptor>
                {
                    IntegrationSupport.SharedCase("Sse", "EventStreamRestWrapper", "SSE stream delivers the expected five events", async (h, ct) =>
                    {
                        using (RestRequest req = new RestRequest(h.Url("/events")))
                        {
                            req.Headers.Add("Accept", "text/event-stream");
                            req.Headers.Add("Cache-Control", "no-cache");
                            using (RestResponse resp = await req.SendAsync())
                            {
                                Check.Equal(200, resp.StatusCode, "sse status");
                                Check.True(resp.ServerSentEvents, "detected as SSE");

                                int count = 0;
                                bool connected = false;
                                bool numbered = false;
                                bool closing = false;
                                while (true)
                                {
                                    ServerSentEvent sse = await resp.ReadEventAsync();
                                    if (sse == null || string.IsNullOrEmpty(sse.Data)) break;
                                    count++;
                                    if (sse.Data.Contains("Connected to")) connected = true;
                                    if (sse.Data.Contains("Event") && sse.Data.Contains("from")) numbered = true;
                                    if (sse.Data.Contains("Connection closing")) closing = true;
                                    if (count > 10) break;
                                }

                                Check.Equal(5, count, "event count");
                                Check.True(connected, "connected event present");
                                Check.True(numbered, "numbered event present");
                                Check.True(closing, "closing event present");
                            }
                        }
                    }),

                    IntegrationSupport.SharedCase("Sse", "EventStreamHttpClient", "SSE stream is readable via HttpClient", async (h, ct) =>
                    {
                        using (HttpClient client = new HttpClient())
                        {
                            using (HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Get, h.Url("/events")))
                            {
                                msg.Headers.Add("Accept", "text/event-stream");
                                using (HttpResponseMessage resp = await client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct))
                                {
                                    Check.True(resp.IsSuccessStatusCode, "sse success");
                                    string mediaType = resp.Content.Headers.ContentType?.MediaType ?? "";
                                    Check.Equal("text/event-stream", mediaType, "content type");

                                    // Read incrementally and stop once the expected events have arrived; do not read to
                                    // end of stream (an SSE stream may stay open under keep-alive).
                                    int dataLines = 0;
                                    using (System.IO.Stream stream = await resp.Content.ReadAsStreamAsync(ct))
                                    using (System.IO.StreamReader reader = new System.IO.StreamReader(stream))
                                    {
                                        string? line;
                                        while ((line = await reader.ReadLineAsync()) != null)
                                        {
                                            if (line.StartsWith("data:", StringComparison.Ordinal)) dataLines++;
                                            if (dataLines >= 5) break;
                                        }
                                    }
                                    Check.True(dataLines >= 5, "at least five data lines (saw " + dataLines + ")");
                                }
                            }
                        }
                    }),

                    IntegrationSupport.SharedCase("Sse", "ConcurrentConnections", "Multiple concurrent SSE connections all deliver events", async (h, ct) =>
                    {
                        Task<int>[] tasks = new Task<int>[3];
                        for (int i = 0; i < tasks.Length; i++)
                        {
                            tasks[i] = Task.Run(async () =>
                            {
                                using (RestRequest req = new RestRequest(h.Url("/events")))
                                {
                                    req.Headers.Add("Accept", "text/event-stream");
                                    using (RestResponse resp = await req.SendAsync())
                                    {
                                        if (resp.StatusCode != 200 || !resp.ServerSentEvents) return 0;
                                        int count = 0;
                                        while (true)
                                        {
                                            ServerSentEvent sse = await resp.ReadEventAsync();
                                            if (sse == null || string.IsNullOrEmpty(sse.Data)) break;
                                            count++;
                                            if (count > 10) break;
                                        }
                                        return count;
                                    }
                                }
                            });
                        }

                        int[] results = await Task.WhenAll(tasks);
                        Check.True(results.Sum() > 0, "events received across connections");
                        Check.True(results.All(r => r == 5), "each connection received five events");
                    })
                });
        }
    }
}
