namespace LoadGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using Switchboard.Core.Client;
    using Switchboard.Core.Models;

    /// <summary>
    /// Produces a realistic-looking topology (origins, endpoints, routes, mappings) and a body of
    /// synthetic request-history rows spread across the configured window. Traffic is shaped by
    /// time-of-day and weekday patterns, per-endpoint weighting, and randomized status codes and
    /// latencies so the dashboard renders like a live deployment rather than uniform noise.
    /// </summary>
    public sealed class SyntheticDataGenerator
    {
        #region Public-Members

        /// <summary>Number of origin servers created.</summary>
        public int OriginsCreated { get; private set; }

        /// <summary>Number of API endpoints created.</summary>
        public int EndpointsCreated { get; private set; }

        /// <summary>Number of endpoint routes created.</summary>
        public int RoutesCreated { get; private set; }

        /// <summary>Number of endpoint-to-origin mappings created.</summary>
        public int MappingsCreated { get; private set; }

        /// <summary>Number of request-history rows inserted.</summary>
        public long HistoryCreated { get; private set; }

        /// <summary>Number of history rows with a successful (2xx/3xx) status.</summary>
        public long SuccessCount { get; private set; }

        /// <summary>Number of history rows with a failed (4xx/5xx) status.</summary>
        public long FailureCount { get; private set; }

        #endregion

        #region Private-Members

        private readonly SwitchboardClient _Client;
        private readonly GeneratorOptions _Options;
        private readonly Random _Random;
        private readonly List<OriginSpec> _Origins;
        private readonly List<EndpointSpec> _Endpoints;

        // Relative traffic weight per hour of day (UTC): quiet overnight, ramping through the working
        // day and easing off in the evening.
        private static readonly double[] _HourWeights =
        {
            0.30, 0.20, 0.15, 0.12, 0.12, 0.18, 0.35, 0.60, 0.90, 1.10, 1.25, 1.30,
            1.25, 1.20, 1.25, 1.30, 1.20, 1.05, 0.90, 0.75, 0.60, 0.50, 0.42, 0.35
        };

        private static readonly string[] _ClientIpPrefixes =
        {
            "203.0.113", "198.51.100", "192.0.2", "45.79.126", "104.28.7", "172.16.24", "10.0.14", "10.0.37"
        };

        private static readonly string[] _UserAgents =
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_4) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148",
            "curl/8.6.0",
            "PostmanRuntime/7.37.0",
            "okhttp/4.12.0",
            "python-requests/2.31.0",
            "Switchboard-Client/5.0.0"
        };

        private static readonly string[] _SearchTerms =
        {
            "invoices", "north+face+jacket", "wireless+headphones", "annual+report", "user+42",
            "refund+policy", "api+keys", "status+page", "kubernetes", "release+notes"
        };

        // Status codes and their base selection weights (before per-method adjustment).
        private static readonly int[] _StatusCodes =
        {
            200, 201, 204, 206, 301, 302, 304, 400, 401, 403, 404, 409, 422, 429, 500, 502, 503, 504
        };

        private static readonly int[] _StatusWeights =
        {
            760, 35, 25, 5, 12, 12, 20, 20, 18, 10, 22, 6, 8, 12, 9, 6, 7, 4
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize a new generator.
        /// </summary>
        /// <param name="client">Connected Switchboard client.</param>
        /// <param name="options">Parsed options controlling the window and density.</param>
        /// <param name="random">Random source (inject a seeded instance for reproducibility).</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public SyntheticDataGenerator(SwitchboardClient client, GeneratorOptions options, Random random)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
            _Options = options ?? throw new ArgumentNullException(nameof(options));
            _Random = random ?? throw new ArgumentNullException(nameof(random));
            _Origins = BuildOrigins();
            _Endpoints = BuildEndpoints();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Seed the topology, then generate and insert the request history.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RunAsync(CancellationToken token)
        {
            await SeedTopologyAsync(token).ConfigureAwait(false);
            await GenerateHistoryAsync(token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods-Topology

        private async Task SeedTopologyAsync(CancellationToken token)
        {
            Dictionary<string, Guid> originGuids = new Dictionary<string, Guid>();

            foreach (OriginSpec origin in _Origins)
            {
                OriginServerConfig config = new OriginServerConfig();
                config.Identifier = origin.Identifier;
                config.Name = origin.Name;
                config.Hostname = origin.Hostname;
                config.Port = origin.Port;
                config.Ssl = origin.Ssl;
                config.HealthCheckMethod = "HEAD";
                config.HealthCheckUrl = "/";
                config.HealthCheckIntervalMs = 30000;
                config.MaxParallelRequests = 64;
                config.RateLimitRequestsThreshold = 512;

                originGuids[origin.Identifier] = config.GUID;

                if (await TryCreateAsync(() => _Client.OriginServers.CreateAsync(config, token)).ConfigureAwait(false))
                    OriginsCreated++;
            }

            foreach (EndpointSpec endpoint in _Endpoints)
            {
                ApiEndpointConfig config = new ApiEndpointConfig();
                config.Identifier = endpoint.Identifier;
                config.Name = endpoint.Name;
                config.LoadBalancingMode = endpoint.LoadBalancing;
                Guid endpointGuid = config.GUID;

                if (await TryCreateAsync(() => _Client.ApiEndpoints.CreateAsync(config, token)).ConfigureAwait(false))
                    EndpointsCreated++;

                int routeSort = 0;
                foreach (RouteSpec route in endpoint.Routes)
                {
                    EndpointRoute row = new EndpointRoute();
                    row.EndpointIdentifier = endpoint.Identifier;
                    row.EndpointGUID = endpointGuid;
                    row.HttpMethod = route.Method;
                    row.UrlPattern = route.UrlPattern;
                    row.RequiresAuthentication = endpoint.Authenticated;
                    row.SortOrder = routeSort++;

                    if (await TryCreateAsync(() => _Client.EndpointRoutes.CreateAsync(row, token)).ConfigureAwait(false))
                        RoutesCreated++;
                }

                int mappingSort = 0;
                foreach (string originIdentifier in endpoint.OriginIdentifiers)
                {
                    if (!originGuids.TryGetValue(originIdentifier, out Guid originGuid)) continue;

                    EndpointOriginMapping mapping = new EndpointOriginMapping();
                    mapping.EndpointIdentifier = endpoint.Identifier;
                    mapping.EndpointGUID = endpointGuid;
                    mapping.OriginIdentifier = originIdentifier;
                    mapping.OriginGUID = originGuid;
                    mapping.SortOrder = mappingSort++;

                    if (await TryCreateAsync(() => _Client.EndpointOriginMappings.CreateAsync(mapping, token)).ConfigureAwait(false))
                        MappingsCreated++;
                }
            }
        }

        // Create helper that tolerates a resource already existing from a prior run, so the tool is
        // safely re-runnable. Any create that fails (typically a duplicate identifier) is treated as
        // "already present" and not counted as newly created.
        private static async Task<bool> TryCreateAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Private-Methods-History

        private async Task GenerateHistoryAsync(CancellationToken token)
        {
            int[] endpointWeights = new int[_Endpoints.Count];
            for (int i = 0; i < _Endpoints.Count; i++) endpointWeights[i] = _Endpoints[i].Weight;

            DateTime firstDay = _Options.StartUtc.Date;
            DateTime lastDay = _Options.EndUtc.Date;
            long reportEvery = 2500;

            for (DateTime day = firstDay; day <= lastDay; day = day.AddDays(1))
            {
                token.ThrowIfCancellationRequested();

                double dayFactor = (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) ? 0.55 : 1.0;
                double jitter = 0.80 + (_Random.NextDouble() * 0.40);
                int dailyCount = (int)Math.Round(_Options.RequestsPerDay * dayFactor * jitter);

                for (int i = 0; i < dailyCount; i++)
                {
                    int hour = WeightedHour();
                    DateTime timestamp = day
                        .AddHours(hour)
                        .AddMinutes(_Random.Next(0, 60))
                        .AddSeconds(_Random.Next(0, 60))
                        .AddMilliseconds(_Random.Next(0, 1000));
                    timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

                    if (timestamp < _Options.StartUtc || timestamp >= _Options.EndUtc) continue;

                    EndpointSpec endpoint = _Endpoints[WeightedIndex(endpointWeights)];
                    RouteSpec route = PickRoute(endpoint);
                    string originIdentifier = endpoint.OriginIdentifiers[_Random.Next(endpoint.OriginIdentifiers.Count)];
                    int statusCode = PickStatusCode(route.Method, endpoint);
                    bool success = statusCode < 400;
                    long durationMs = PickLatencyMs(statusCode, BaseLatencyMs(endpoint.Identifier));
                    string path = ConcretePath(route.UrlPattern);
                    string? query = BuildQueryString(endpoint, route);
                    string clientIp = RandomClientIp();
                    bool authenticated = endpoint.Authenticated ? (_Random.NextDouble() < 0.97) : (_Random.NextDouble() < 0.15);

                    RequestHistory history = new RequestHistory();
                    history.RequestId = Guid.NewGuid();
                    history.GUID = Guid.NewGuid();
                    history.TimestampUtc = timestamp;
                    history.HttpMethod = route.Method;
                    history.RequestPath = path;
                    history.QueryString = query;
                    history.EndpointIdentifier = endpoint.Identifier;
                    history.EndpointGUID = DeterministicEndpointGuid(endpoint.Identifier);
                    history.OriginIdentifier = success || statusCode >= 500 ? originIdentifier : (statusCode == 404 ? null : originIdentifier);
                    history.OriginGUID = history.OriginIdentifier != null ? DeterministicOriginGuid(originIdentifier) : (Guid?)null;
                    history.ClientIp = clientIp;
                    history.RequestBodySize = RequestBodySize(route.Method);
                    history.RequestHeaders = BuildRequestHeaders(clientIp, authenticated);
                    history.StatusCode = statusCode;
                    history.ResponseBodySize = ResponseBodySize(statusCode);
                    history.ResponseHeaders = BuildResponseHeaders(statusCode, originIdentifier);
                    history.DurationMs = durationMs;
                    history.WasAuthenticated = authenticated;
                    history.ErrorMessage = success ? null : ErrorMessageFor(statusCode);
                    history.Success = success;

                    // Insert through the low-level driver rather than RequestHistory.CreateAsync,
                    // which stamps TimestampUtc with the current time (correct for live capture, but it
                    // would discard the backdated timestamps that spread this data across the window).
                    await _Client.Database.InsertAsync(history, token).ConfigureAwait(false);

                    HistoryCreated++;
                    if (success) SuccessCount++; else FailureCount++;

                    if (HistoryCreated % reportEvery == 0)
                        Console.Error.WriteLine("  ... " + HistoryCreated.ToString("N0", CultureInfo.InvariantCulture) + " request history rows inserted");
                }
            }
        }

        private int WeightedHour()
        {
            double total = 0.0;
            for (int h = 0; h < _HourWeights.Length; h++) total += _HourWeights[h];
            double pick = _Random.NextDouble() * total;
            double running = 0.0;
            for (int h = 0; h < _HourWeights.Length; h++)
            {
                running += _HourWeights[h];
                if (pick <= running) return h;
            }
            return _HourWeights.Length - 1;
        }

        private int WeightedIndex(int[] weights)
        {
            int total = 0;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            int pick = _Random.Next(total);
            int running = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                running += weights[i];
                if (pick < running) return i;
            }
            return weights.Length - 1;
        }

        private RouteSpec PickRoute(EndpointSpec endpoint)
        {
            int[] weights = new int[endpoint.Routes.Count];
            for (int i = 0; i < endpoint.Routes.Count; i++) weights[i] = endpoint.Routes[i].Weight;
            return endpoint.Routes[WeightedIndex(weights)];
        }

        private int PickStatusCode(string method, EndpointSpec endpoint)
        {
            int status = _StatusCodes[WeightedIndex(_StatusWeights)];

            // Nudge 2xx codes toward the method-appropriate variant.
            if (status == 200)
            {
                if (method == "POST" && _Random.NextDouble() < 0.45) status = 201;
                else if (method == "DELETE" && _Random.NextDouble() < 0.55) status = 204;
                else if ((method == "PUT" || method == "PATCH") && _Random.NextDouble() < 0.20) status = 204;
            }

            // Authentication endpoints reject a little more often.
            if (endpoint.Authenticated && status == 200 && _Random.NextDouble() < 0.05) status = 401;

            return status;
        }

        private long PickLatencyMs(int statusCode, int baseMs)
        {
            if (statusCode == 504) return 3000 + _Random.Next(0, 5000);
            if (statusCode >= 500) return LogNormal(baseMs * 2.2, 0.7);
            if (statusCode == 429) return Math.Max(1, (long)Math.Round(baseMs * 0.3));
            if (statusCode >= 300 && statusCode < 400) return Math.Max(1, (long)Math.Round(baseMs * 0.6));
            return LogNormal(baseMs, 0.55);
        }

        private long LogNormal(double baseMs, double sigma)
        {
            double value = baseMs * Math.Exp(sigma * NextGaussian());
            long rounded = (long)Math.Round(value);
            return rounded < 1 ? 1 : rounded;
        }

        private double NextGaussian()
        {
            double u1 = 1.0 - _Random.NextDouble();
            double u2 = 1.0 - _Random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        private string ConcretePath(string pattern)
        {
            return Regex.Replace(pattern, "\\{[^}]+\\}", match => _Random.Next(1, 100000).ToString(CultureInfo.InvariantCulture));
        }

        private string? BuildQueryString(EndpointSpec endpoint, RouteSpec route)
        {
            if (route.UrlPattern.Contains("/search"))
                return "q=" + _SearchTerms[_Random.Next(_SearchTerms.Length)];

            if (route.Method == "GET" && !route.UrlPattern.Contains("{") && _Random.NextDouble() < 0.35)
                return "page=" + _Random.Next(1, 25) + "&limit=" + (new int[] { 10, 20, 50, 100 })[_Random.Next(4)];

            return null;
        }

        private string RandomClientIp()
        {
            string prefix = _ClientIpPrefixes[_Random.Next(_ClientIpPrefixes.Length)];
            return prefix + "." + _Random.Next(1, 255).ToString(CultureInfo.InvariantCulture);
        }

        private long RequestBodySize(string method)
        {
            if (method == "POST" || method == "PUT" || method == "PATCH") return _Random.Next(120, 4096);
            return 0;
        }

        private long ResponseBodySize(int statusCode)
        {
            if (statusCode == 204 || statusCode == 304 || (statusCode >= 300 && statusCode < 400)) return 0;
            if (statusCode >= 400) return _Random.Next(80, 640);
            return _Random.Next(200, 12288);
        }

        private string BuildRequestHeaders(string clientIp, bool authenticated)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Accept"] = "application/json";
            headers["User-Agent"] = _UserAgents[_Random.Next(_UserAgents.Length)];
            headers["Accept-Encoding"] = "gzip, deflate, br";
            headers["X-Forwarded-For"] = clientIp;
            if (authenticated) headers["Authorization"] = "Bearer ****************";
            return JsonSerializer.Serialize(headers);
        }

        private string BuildResponseHeaders(int statusCode, string originIdentifier)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Content-Type"] = (statusCode == 204 || statusCode == 304) ? "text/plain" : "application/json; charset=utf-8";
            headers["Server"] = "switchboard";
            headers["X-Origin-Server"] = originIdentifier;
            if (statusCode == 429) headers["Retry-After"] = _Random.Next(1, 10).ToString(CultureInfo.InvariantCulture);
            return JsonSerializer.Serialize(headers);
        }

        private static string ErrorMessageFor(int statusCode)
        {
            switch (statusCode)
            {
                case 400: return "Bad request: the request body could not be parsed.";
                case 401: return "Authentication failed.";
                case 403: return "Authorization failed.";
                case 404: return "No matching API endpoint found.";
                case 409: return "The request conflicts with the current state.";
                case 422: return "The request could not be processed.";
                case 429: return "Rate limit exceeded for the origin server.";
                case 500: return "The origin server returned an internal error.";
                case 502: return "No healthy origin servers available.";
                case 503: return "The origin server is temporarily unavailable.";
                case 504: return "The origin server request timed out.";
                default: return "Request failed with status " + statusCode + ".";
            }
        }

        private static int BaseLatencyMs(string endpointIdentifier)
        {
            switch (endpointIdentifier)
            {
                case "auth": return 22;
                case "users-api": return 30;
                case "catalog-api": return 38;
                case "orders-api": return 46;
                case "search": return 85;
                case "media": return 120;
                default: return 40;
            }
        }

        private static Guid DeterministicEndpointGuid(string identifier)
        {
            ApiEndpointConfig config = new ApiEndpointConfig();
            config.Identifier = identifier;
            return config.GUID;
        }

        private static Guid DeterministicOriginGuid(string identifier)
        {
            OriginServerConfig config = new OriginServerConfig();
            config.Identifier = identifier;
            return config.GUID;
        }

        #endregion

        #region Private-Methods-Topology-Templates

        private static List<OriginSpec> BuildOrigins()
        {
            return new List<OriginSpec>
            {
                new OriginSpec("gateway-us-east-1", "Gateway US East 1", "api-use1.internal.svc", 8080, false),
                new OriginSpec("gateway-us-west-2", "Gateway US West 2", "api-usw2.internal.svc", 8080, false),
                new OriginSpec("gateway-eu-west-1", "Gateway EU West 1", "api-euw1.internal.svc", 8080, false),
                new OriginSpec("auth-service", "Authentication Service", "auth.internal.svc", 9000, true),
                new OriginSpec("search-cluster", "Search Cluster", "search.internal.svc", 9200, false),
                new OriginSpec("media-store", "Media Store", "media.internal.svc", 8443, true)
            };
        }

        private static List<EndpointSpec> BuildEndpoints()
        {
            List<EndpointSpec> endpoints = new List<EndpointSpec>();

            endpoints.Add(new EndpointSpec(
                "users-api", "Users API", "RoundRobin", 24, true,
                new List<RouteSpec>
                {
                    new RouteSpec("GET", "/api/users", 30),
                    new RouteSpec("GET", "/api/users/{id}", 40),
                    new RouteSpec("POST", "/api/users", 10),
                    new RouteSpec("PUT", "/api/users/{id}", 8),
                    new RouteSpec("DELETE", "/api/users/{id}", 4)
                },
                new List<string> { "gateway-us-east-1", "gateway-us-west-2", "gateway-eu-west-1" }));

            endpoints.Add(new EndpointSpec(
                "orders-api", "Orders API", "RoundRobin", 20, true,
                new List<RouteSpec>
                {
                    new RouteSpec("GET", "/api/orders", 26),
                    new RouteSpec("GET", "/api/orders/{id}", 34),
                    new RouteSpec("POST", "/api/orders", 18),
                    new RouteSpec("PATCH", "/api/orders/{id}", 10)
                },
                new List<string> { "gateway-us-east-1", "gateway-us-west-2" }));

            endpoints.Add(new EndpointSpec(
                "auth", "Authentication", "Random", 18, false,
                new List<RouteSpec>
                {
                    new RouteSpec("POST", "/auth/login", 34),
                    new RouteSpec("POST", "/auth/refresh", 24),
                    new RouteSpec("POST", "/auth/logout", 12),
                    new RouteSpec("GET", "/auth/me", 30)
                },
                new List<string> { "auth-service" }));

            endpoints.Add(new EndpointSpec(
                "catalog-api", "Catalog API", "RoundRobin", 16, false,
                new List<RouteSpec>
                {
                    new RouteSpec("GET", "/api/products", 30),
                    new RouteSpec("GET", "/api/products/{id}", 40),
                    new RouteSpec("GET", "/api/categories", 20)
                },
                new List<string> { "gateway-us-east-1", "gateway-us-west-2", "gateway-eu-west-1" }));

            endpoints.Add(new EndpointSpec(
                "search", "Search API", "Random", 14, false,
                new List<RouteSpec>
                {
                    new RouteSpec("GET", "/api/search", 70),
                    new RouteSpec("GET", "/api/search/suggest", 30)
                },
                new List<string> { "search-cluster" }));

            endpoints.Add(new EndpointSpec(
                "media", "Media API", "RoundRobin", 8, true,
                new List<RouteSpec>
                {
                    new RouteSpec("GET", "/media/{id}", 72),
                    new RouteSpec("POST", "/media/upload", 28)
                },
                new List<string> { "media-store" }));

            return endpoints;
        }

        #endregion
    }
}
