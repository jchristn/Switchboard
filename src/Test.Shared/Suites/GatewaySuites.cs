namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    using RestWrapper;
    using Switchboard.Core;
    using Test.Shared.Harness;
    using Touchstone.Core;
    using WatsonWebserver.Core;

    using AuthenticationResultEnum = Switchboard.Core.AuthenticationResultEnum;
    using AuthorizationResultEnum = Switchboard.Core.AuthorizationResultEnum;
    using HttpMethod = System.Net.Http.HttpMethod;

    /// <summary>
    /// Integration suite for gateway error and policy branches that require mutating endpoint
    /// configuration or the auth callback: HTTP/1.0 blocking (505), request-size limits (413), and
    /// the "success in either auth dimension passes" rule. Runs against a dedicated harness so the
    /// mutations never affect other suites; each case restores what it changed.
    /// </summary>
    public static class GatewaySuites
    {
        /// <summary>
        /// All gateway policy suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the gateway policy suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Gateway",
                displayName: "Gateway Policy Branches",
                beforeSuiteAsync: ct => new ValueTask(TestHarnesses.GatewayErrors.StartAsync(ct)),
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Gateway", "Http10Blocked", "HTTP/1.0 request is rejected with 505 when blocked",
                        executeAsync: async ct =>
                        {
                            ProxyHarness h = TestHarnesses.GatewayErrors;
                            await h.StartAsync(ct).ConfigureAwait(false);
                            ApiEndpoint endpoint = h.Settings.Endpoints[0];
                            endpoint.BlockHttp10 = true;
                            try
                            {
                                using (TcpClient tcp = new TcpClient())
                                {
                                    await tcp.ConnectAsync("localhost", h.ProxyPort).ConfigureAwait(false);
                                    using (NetworkStream ns = tcp.GetStream())
                                    {
                                        byte[] request = Encoding.ASCII.GetBytes("GET /unauthenticated HTTP/1.0\r\nHost: localhost\r\n\r\n");
                                        await ns.WriteAsync(request, 0, request.Length, ct).ConfigureAwait(false);
                                        await ns.FlushAsync(ct).ConfigureAwait(false);

                                        byte[] buffer = new byte[2048];
                                        int read = await ns.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                                        string response = Encoding.ASCII.GetString(buffer, 0, read);
                                        Check.Contains(response, "505", "HTTP/1.0 blocked with 505");
                                    }
                                }
                            }
                            finally
                            {
                                endpoint.BlockHttp10 = false;
                            }
                        }),

                    new TestCaseDescriptor("Gateway", "RequestTooLarge", "Request exceeding MaxRequestBodySize returns 413 with a TooLarge body",
                        executeAsync: async ct =>
                        {
                            ProxyHarness h = TestHarnesses.GatewayErrors;
                            await h.StartAsync(ct).ConfigureAwait(false);
                            ApiEndpoint endpoint = h.Settings.Endpoints[0];
                            int original = endpoint.MaxRequestBodySize;
                            endpoint.MaxRequestBodySize = 10;
                            try
                            {
                                using (RestRequest req = new RestRequest(h.Url("/api/users"), HttpMethod.Post))
                                {
                                    req.ContentType = "text/plain";
                                    using (RestResponse resp = await req.SendAsync(new string('x', 500)))
                                    {
                                        Check.Equal(413, resp.StatusCode, "oversized body returns 413");
                                        Check.Contains(resp.DataAsString, "TooLarge", "TooLarge error body");
                                    }
                                }
                            }
                            finally
                            {
                                endpoint.MaxRequestBodySize = original;
                            }
                        }),

                    new TestCaseDescriptor("Gateway", "AuthEitherDimensionPasses", "Success in either auth dimension is allowed through",
                        executeAsync: async ct =>
                        {
                            ProxyHarness h = TestHarnesses.GatewayErrors;
                            await h.StartAsync(ct).ConfigureAwait(false);
                            SwitchboardDaemon daemon = h.Daemon!;
                            daemon.Callbacks.AuthenticateAndAuthorize = _ =>
                            {
                                AuthContext result = new AuthContext();
                                result.Authentication.Result = AuthenticationResultEnum.Success;
                                result.Authorization.Result = AuthorizationResultEnum.Denied;
                                return Task.FromResult(result);
                            };
                            try
                            {
                                using (RestRequest req = new RestRequest(h.Url("/authenticated")))
                                using (RestResponse resp = await req.SendAsync())
                                    Check.Equal(200, resp.StatusCode, "authenticated-only success passes");
                            }
                            finally
                            {
                                daemon.Callbacks.AuthenticateAndAuthorize = AuthCallbacks.AuthenticateAndAuthorize;
                            }
                        })
                });
        }
    }
}
