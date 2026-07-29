namespace Test.Shared.Harness
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using Switchboard.Core;
    using Switchboard.Core.Settings;
    using WatsonWebserver.Core;

    /// <summary>
    /// Boots a <see cref="SwitchboardDaemon"/> in front of one or more <see cref="OriginHost"/>
    /// backends on real localhost ports, and waits for the origins to become healthy. Starting is
    /// idempotent and thread-safe so the same instance can back many test cases regardless of the
    /// runner (console, xUnit, NUnit) and regardless of whether suite lifecycle hooks fire.
    /// </summary>
    public sealed class ProxyHarness : IDisposable
    {
        private readonly IReadOnlyList<string> _OriginNames;
        private int _ProxyPort;
        private IReadOnlyList<KeyValuePair<string, int>> _OriginPorts = new List<KeyValuePair<string, int>>();
        private readonly int _MaxParallelRequests;
        private readonly int _RateLimitThreshold;
        private readonly bool _EnableManagement;
        private readonly string _DbPath;
        private readonly SemaphoreSlim _StartLock = new SemaphoreSlim(1, 1);
        private readonly List<OriginHost> _Origins = new List<OriginHost>();

        private SwitchboardSettings _Settings = null!;
        private SwitchboardDaemon? _Daemon;
        private bool _Started;
        private bool _Disposed;

        /// <summary>
        /// Initialize a harness definition. No ports are bound until <see cref="StartAsync"/> runs, at
        /// which point a random free localhost port is allocated for the proxy and for each origin.
        /// </summary>
        /// <param name="originNames">Origin server names (for example "Server 1"). One backend is created per name.</param>
        /// <param name="maxParallelRequests">Per-origin parallel request cap.</param>
        /// <param name="rateLimitThreshold">Per-origin rate limit threshold.</param>
        /// <param name="enableManagement">True to enable the management REST API on the daemon.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="originNames"/> is null.</exception>
        public ProxyHarness(
            IReadOnlyList<string> originNames,
            int maxParallelRequests = 100,
            int rateLimitThreshold = 1000,
            bool enableManagement = false)
        {
            _OriginNames = originNames ?? throw new ArgumentNullException(nameof(originNames));
            _MaxParallelRequests = maxParallelRequests;
            _RateLimitThreshold = rateLimitThreshold;
            _EnableManagement = enableManagement;
            _DbPath = Path.Combine(Path.GetTempPath(), "switchboard_harness_" + Guid.NewGuid().ToString("N") + ".db");
        }

        /// <summary>
        /// The port the proxy listens on.
        /// </summary>
        public int ProxyPort
        {
            get { return _ProxyPort; }
        }

        /// <summary>
        /// The settings the daemon was constructed with. Populated after <see cref="StartAsync"/>.
        /// </summary>
        public SwitchboardSettings Settings
        {
            get { return _Settings; }
        }

        /// <summary>
        /// The running origin hosts.
        /// </summary>
        public IReadOnlyList<OriginHost> Origins
        {
            get { return _Origins; }
        }

        /// <summary>
        /// The running daemon. Populated after <see cref="StartAsync"/>.
        /// </summary>
        public SwitchboardDaemon? Daemon
        {
            get { return _Daemon; }
        }

        /// <summary>
        /// Build an absolute proxy URL for the given path (and optional query).
        /// </summary>
        /// <param name="pathAndQuery">Path beginning with '/'.</param>
        /// <returns>Absolute URL against the proxy.</returns>
        public string Url(string pathAndQuery)
        {
            return "http://127.0.0.1:" + _ProxyPort + pathAndQuery;
        }

        /// <summary>
        /// Start the origins and daemon (idempotent) and wait for all origins to become healthy.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public async Task StartAsync(CancellationToken token = default)
        {
            if (_Started) return;

            await _StartLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_Started) return;

                // Allocate a random free localhost port for the proxy and for each origin so parallel
                // runs and leftover processes never collide on a fixed port.
                _ProxyPort = FreeTcpPort();
                List<KeyValuePair<string, int>> originPorts = new List<KeyValuePair<string, int>>();
                foreach (string name in _OriginNames)
                    originPorts.Add(new KeyValuePair<string, int>(name, FreeTcpPort()));
                _OriginPorts = originPorts;

                _Settings = BuildSettings();

                foreach (KeyValuePair<string, int> kvp in _OriginPorts)
                {
                    OriginHost origin = new OriginHost(kvp.Key, kvp.Value);
                    origin.Start();
                    _Origins.Add(origin);
                }

                _Daemon = new SwitchboardDaemon(_Settings);
                _Daemon.Callbacks.AuthenticateAndAuthorize = AuthCallbacks.AuthenticateAndAuthorize;

                await WaitForAllHealthyAsync(TimeSpan.FromSeconds(30), token).ConfigureAwait(false);

                _Started = true;
            }
            finally
            {
                _StartLock.Release();
            }
        }

        /// <summary>
        /// Wait until every origin is reported healthy, or throw on timeout.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task WaitForAllHealthyAsync(TimeSpan timeout, CancellationToken token = default)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (_Settings.Origins.Any(o => !o.Healthy))
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Not all origin servers became healthy within " + timeout.TotalSeconds + "s.");
                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }

        private static int FreeTcpPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private SwitchboardSettings BuildSettings()
        {
            SwitchboardSettings settings = new SwitchboardSettings();

            // Use the IPv4 loopback, not "localhost". On Windows "localhost" resolves to IPv6 (::1)
            // first, and every connection to an IPv4-only listener waits out a failed ::1 connect
            // (~2s) before falling back — paid on both the client->proxy and proxy->origin hops, which
            // is what made the integration suite slow.
            settings.Webserver.Hostname = "127.0.0.1";
            settings.Webserver.Port = _ProxyPort;

            settings.Logging.ConsoleLogging = false;
            settings.Logging.EnableColors = false;
            settings.Logging.MinimumSeverity = 7;

            settings.Database.Type = Switchboard.Core.Database.DatabaseTypeEnum.Sqlite;
            settings.Database.Filename = _DbPath;

            settings.Management.Enable = _EnableManagement;
            settings.RequestHistory.Enable = false;

            // A custom global blocked header used to verify that configured blocked headers are
            // stripped before forwarding to origins (the origin echoes X-* headers it receives).
            settings.BlockedHeaders.Add("x-blocked-secret");

            settings.OpenApi = new OpenApiDocumentSettings
            {
                Enable = true,
                EnableSwaggerUi = true,
                DocumentPath = "/openapi.json",
                SwaggerUiPath = "/swagger",
                Title = "Switchboard Test API",
                Version = "1.0.0",
                Description = "Test API for Switchboard reverse proxy",
                Contact = new OpenApiContactSettings { Name = "Test Support", Email = "test@example.com" },
                SecuritySchemes = new Dictionary<string, OpenApiSecuritySchemeSettings>
                {
                    ["bearerAuth"] = new OpenApiSecuritySchemeSettings
                    {
                        Type = "http",
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "JWT Bearer token authentication"
                    }
                },
                Tags = new List<OpenApiTagSettings>
                {
                    new OpenApiTagSettings { Name = "Users", Description = "User management operations" },
                    new OpenApiTagSettings { Name = "Data", Description = "Data operations" },
                    new OpenApiTagSettings { Name = "Events", Description = "Server-Sent Events" }
                }
            };

            List<string> originIds = _OriginPorts.Select(o => o.Key).ToList();

            settings.Endpoints.Add(new ApiEndpoint
            {
                Identifier = "test-endpoint",
                Name = "Test Endpoint",
                LoadBalancing = LoadBalancingMode.RoundRobin,
                AuthContextHeader = Constants.AuthContextHeader,
                IncludeAuthContextHeader = true,
                TimeoutMs = 30000,
                MaxRequestBodySize = 10485760,
                BlockHttp10 = false,
                Unauthenticated = new ApiEndpointGroup
                {
                    ParameterizedUrls = new Dictionary<string, List<string>>
                    {
                        { "GET", new List<string> { "/unauthenticated", "/api/users", "/api/data", "/events", "/chunked-download", "/api/v2/users/{userId}", "/api/headers-test", "/api/query-test", "/api/timeout-test", "/api/large-body-test" } },
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
                    { "GET", new Dictionary<string, string> { { "/api/v2/users/{userId}", "/v1/users/{userId}" } } }
                },
                OriginServers = originIds,
                OpenApiDocumentation = new OpenApiEndpointMetadata
                {
                    Routes = new Dictionary<string, Dictionary<string, OpenApiRouteDocumentation>>
                    {
                        ["GET"] = new Dictionary<string, OpenApiRouteDocumentation>
                        {
                            ["/api/users"] = new OpenApiRouteDocumentation
                            {
                                OperationId = "getUsers",
                                Summary = "List all users",
                                Description = "Returns a list of all users in the system",
                                Tags = new List<string> { "Users" },
                                Responses = new Dictionary<string, OpenApiResponseDocumentation>
                                {
                                    ["200"] = new OpenApiResponseDocumentation
                                    {
                                        Description = "Successful response",
                                        Content = new Dictionary<string, OpenApiMediaTypeDocumentation>
                                        {
                                            ["application/json"] = new OpenApiMediaTypeDocumentation { SchemaType = "array" }
                                        }
                                    }
                                }
                            },
                            ["/events"] = new OpenApiRouteDocumentation
                            {
                                OperationId = "getEvents",
                                Summary = "Subscribe to event stream",
                                Description = "Opens a Server-Sent Events connection",
                                Tags = new List<string> { "Events" },
                                Responses = new Dictionary<string, OpenApiResponseDocumentation>
                                {
                                    ["200"] = new OpenApiResponseDocumentation { Description = "Event stream opened" }
                                }
                            }
                        },
                        ["POST"] = new Dictionary<string, OpenApiRouteDocumentation>
                        {
                            ["/api/users"] = new OpenApiRouteDocumentation
                            {
                                OperationId = "createUser",
                                Summary = "Create a new user",
                                Description = "Creates a new user account",
                                Tags = new List<string> { "Users" },
                                RequestBody = new OpenApiRequestBodyDocumentation
                                {
                                    Description = "User data",
                                    Required = true,
                                    Content = new Dictionary<string, OpenApiMediaTypeDocumentation>
                                    {
                                        ["application/json"] = new OpenApiMediaTypeDocumentation { SchemaType = "object" }
                                    }
                                },
                                Responses = new Dictionary<string, OpenApiResponseDocumentation>
                                {
                                    ["201"] = new OpenApiResponseDocumentation { Description = "User created" },
                                    ["400"] = new OpenApiResponseDocumentation { Description = "Invalid request" }
                                }
                            }
                        }
                    }
                }
            });

            foreach (KeyValuePair<string, int> kvp in _OriginPorts)
            {
                settings.Origins.Add(new OriginServer
                {
                    Identifier = kvp.Key,
                    Name = kvp.Key,
                    Hostname = "127.0.0.1",
                    Port = kvp.Value,
                    Ssl = false,
                    HealthCheckIntervalMs = 1000,
                    UnhealthyThreshold = 2,
                    HealthyThreshold = 2,
                    MaxParallelRequests = _MaxParallelRequests,
                    RateLimitRequestsThreshold = _RateLimitThreshold
                });
            }

            return settings;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            try
            {
                _Daemon?.Dispose();
            }
            catch (Exception)
            {
                // ignore
            }

            foreach (OriginHost origin in _Origins)
            {
                origin.Dispose();
            }

            _Origins.Clear();
            _StartLock.Dispose();

            try
            {
                if (File.Exists(_DbPath)) File.Delete(_DbPath);
            }
            catch (Exception)
            {
                // best-effort cleanup
            }
        }
    }
}
