namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Switchboard.Core;
    using Switchboard.Core.Services;
    using Touchstone.Core;
    using UrlMatcher;

    /// <summary>
    /// Network-free coverage for <see cref="RouteMatcher"/> and catch-all ({*name}) routing: pattern
    /// validation, endpoint precedence (routes without a catch-all first, then the most specific
    /// catch-all), captured values, URL rewrites, invalid-pattern handling, and OpenAPI generation.
    /// </summary>
    public static class RouteMatcherSuites
    {
        private const string SuiteId = "RouteMatcher";

        /// <summary>
        /// All route matcher suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the route matcher suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Route Matcher (catch-all routes, precedence, validation)",
                cases: new List<TestCaseDescriptor>
                {
                    // ---- Pattern validation ----
                    Case("ParseAcceptsCatchAll", "TryParsePattern accepts a trailing {*name} and reports it", () =>
                    {
                        Check.True(RouteMatcher.TryParsePattern("/api/{*rest}", out UrlPattern p, out string error), "valid");
                        Check.True(error == null, "no error");
                        Check.True(p.IsCatchAll, "is catch-all");
                        Check.Equal("rest", p.CatchAllName, "catch-all name");
                        Check.Equal(1, p.LiteralPrefixCount, "literal prefix");
                    }),

                    Case("ParseAcceptsOrdinaryPatterns", "TryParsePattern accepts literal and parameter patterns", () =>
                    {
                        foreach (string pattern in new[] { "/", "/api/users", "/api/users/{id}", "/{v}/files/{*path}", "/a/{*}", "/a/*", "/a/**", "/a/{}" })
                        {
                            Check.True(RouteMatcher.TryParsePattern(pattern, out UrlPattern p, out _), "valid: " + pattern);
                            Check.True(p != null, "parsed: " + pattern);
                        }
                    }),

                    Case("ParseLiteralStarBracesNotCatchAll", "{*}, *, and ** are literals, not catch-alls", () =>
                    {
                        foreach (string pattern in new[] { "/a/{*}", "/a/*", "/a/**" })
                        {
                            RouteMatcher.TryParsePattern(pattern, out UrlPattern p, out _);
                            Check.False(p.IsCatchAll, pattern + " is not a catch-all");
                        }
                    }),

                    Case("ParseRejectsCatchAllNotLast", "A catch-all that is not the last segment is invalid", () =>
                    {
                        Check.False(RouteMatcher.TryParsePattern("/{*rest}/edit", out UrlPattern p, out string error), "invalid");
                        Check.True(p == null, "no parsed pattern");
                        Check.Contains(error, "last segment", "error explains the rule");
                        Check.False(error.Contains("(Parameter"), "parameter-name suffix removed from the message");
                    }),

                    Case("ParseRejectsMultipleCatchAlls", "More than one catch-all is invalid", () =>
                    {
                        Check.False(RouteMatcher.TryParsePattern("/{*a}/{*b}", out _, out string error), "invalid");
                        Check.Contains(error, "only one catch-all", "error explains the rule");
                    }),

                    Case("ParseRejectsPartialCatchAll", "A catch-all inside a larger segment is invalid", () =>
                    {
                        Check.False(RouteMatcher.TryParsePattern("/files/v{*rest}", out _, out string error), "invalid");
                        Check.Contains(error, "entire segment", "error explains the rule");
                    }),

                    Case("ParseRejectsEmpty", "Null, empty, and whitespace patterns are invalid", () =>
                    {
                        foreach (string? pattern in new string?[] { null, "", "   " })
                        {
                            Check.False(RouteMatcher.TryParsePattern(pattern!, out _, out string error), "invalid: [" + pattern + "]");
                            Check.Equal("URL pattern is required.", error, "error for [" + pattern + "]");
                        }
                    }),

                    Case("ParseIsCachedAndStable", "Parsing the same pattern twice returns the same instance and the same error", () =>
                    {
                        RouteMatcher.TryParsePattern("/cache/{*rest}", out UrlPattern a, out _);
                        RouteMatcher.TryParsePattern("/cache/{*rest}", out UrlPattern b, out _);
                        Check.True(ReferenceEquals(a, b), "valid pattern cached");
                        RouteMatcher.TryParsePattern("/cache/{*x}/y", out _, out string e1);
                        RouteMatcher.TryParsePattern("/cache/{*x}/y", out _, out string e2);
                        Check.Equal(e1, e2, "invalid pattern error cached");
                    }),

                    Case("MaxCachedPatternsClamped", "MaxCachedPatterns clamps values below 1", () =>
                    {
                        int original = RouteMatcher.MaxCachedPatterns;
                        try
                        {
                            RouteMatcher.MaxCachedPatterns = 0;
                            Check.Equal(1, RouteMatcher.MaxCachedPatterns, "clamped to 1");
                            Check.True(RouteMatcher.TryParsePattern("/clamp/{*r}", out _, out _), "parsing still works with a tiny cache");
                            Check.True(RouteMatcher.TryParsePattern("/clamp/other", out _, out _), "cache clears and refills");
                        }
                        finally
                        {
                            RouteMatcher.MaxCachedPatterns = original;
                        }
                    }),

                    // ---- Matching and captured values ----
                    Case("CatchAllMatchesDeepPath", "/api/{*rest} matches a deep path and captures the remainder", () =>
                    {
                        MatchingApiEndpoint? m = Find(Endpoints(Ep("a", "GET", "/api/{*rest}")), "GET", "/api/users/42/orders");
                        Check.True(m != null, "matched");
                        Check.Equal("a", m!.Endpoint.Identifier, "endpoint");
                        Check.Equal("/api/{*rest}", m.ParameterizedUrl, "pattern");
                        Check.Equal("users/42/orders", m.Parameters["rest"], "captured remainder");
                        Check.Equal("users/42/orders", m.Parameters["REST"], "capture name is case-insensitive");
                    }),

                    Case("CatchAllMatchesPrefixItself", "/api/{*rest} matches /api and /api/ with an empty remainder", () =>
                    {
                        List<ApiEndpoint> eps = Endpoints(Ep("a", "GET", "/api/{*rest}"));
                        foreach (string url in new[] { "/api", "/api/" })
                        {
                            MatchingApiEndpoint? m = Find(eps, "GET", url);
                            Check.True(m != null, "matched " + url);
                            Check.Equal("", m!.Parameters["rest"], "empty remainder for " + url);
                        }
                    }),

                    Case("RootCatchAllMatchesEverything", "/{*path} matches the root and any path", () =>
                    {
                        List<ApiEndpoint> eps = Endpoints(Ep("root", "GET", "/{*path}"));
                        Check.Equal("", Find(eps, "GET", "/")!.Parameters["path"], "root remainder");
                        Check.Equal("a/b/c", Find(eps, "GET", "/a/b/c")!.Parameters["path"], "deep remainder");
                    }),

                    Case("CatchAllRemainderIsRaw", "The remainder keeps repeated and trailing slashes and is not decoded", () =>
                    {
                        List<ApiEndpoint> eps = Endpoints(Ep("a", "GET", "/api/{*rest}"));
                        Check.Equal("a//b/", Find(eps, "GET", "/api/a//b/")!.Parameters["rest"], "slashes kept");
                        Check.Equal("a%2Fb/c", Find(eps, "GET", "/api/a%2Fb/c")!.Parameters["rest"], "not decoded");
                    }),

                    Case("CatchAllIgnoresQueryAndFragment", "The query string and fragment are not part of the remainder", () =>
                    {
                        MatchingApiEndpoint? m = Find(Endpoints(Ep("a", "GET", "/api/{*rest}")), "GET", "/api/a/b?x=1&y=2#frag");
                        Check.Equal("a/b", m!.Parameters["rest"], "remainder without query or fragment");
                    }),

                    Case("ParametersBeforeCatchAll", "Parameters before a catch-all are captured too", () =>
                    {
                        MatchingApiEndpoint? m = Find(Endpoints(Ep("a", "GET", "/{v}/files/{*path}")), "GET", "/v1/files/docs/readme.md");
                        Check.Equal("v1", m!.Parameters["v"], "version parameter");
                        Check.Equal("docs/readme.md", m.Parameters["path"], "path remainder");
                    }),

                    Case("CatchAllLiteralsCaseSensitive", "Literal segments before a catch-all stay case-sensitive", () =>
                    {
                        Check.True(Find(Endpoints(Ep("a", "GET", "/api/{*rest}")), "GET", "/API/users") == null, "no match on different case");
                    }),

                    Case("CatchAllOtherPrefixNoMatch", "A catch-all does not match a different prefix", () =>
                    {
                        Check.True(Find(Endpoints(Ep("a", "GET", "/api/{*rest}")), "GET", "/other/users") == null, "no match");
                        Check.True(Find(Endpoints(Ep("a", "GET", "/api/{*rest}")), "GET", "/apiv2/users") == null, "prefix is segment-based, not string-based");
                    }),

                    Case("CatchAllRequiresMethod", "A GET catch-all does not serve other methods", () =>
                    {
                        List<ApiEndpoint> eps = Endpoints(Ep("a", "GET", "/api/{*rest}"));
                        Check.True(Find(eps, "POST", "/api/users") == null, "POST not matched");
                        Check.True(Find(eps, "get", "/api/users") == null, "method comparison is exact (upper-case)");
                    }),

                    Case("AuthenticatedCatchAllRequiresAuth", "A catch-all in the authenticated group is flagged AuthRequired", () =>
                    {
                        ApiEndpoint ep = new ApiEndpoint { Identifier = "secure" };
                        ep.Authenticated.ParameterizedUrls["GET"] = new List<string> { "/secure/{*rest}" };
                        MatchingApiEndpoint? m = Find(Endpoints(ep), "GET", "/secure/a/b");
                        Check.True(m!.AuthRequired, "auth required");
                        Check.Equal("a/b", m.Parameters["rest"], "remainder");
                    }),

                    Case("OrdinaryRoutesUnchanged", "Routes without a catch-all behave exactly as before", () =>
                    {
                        List<ApiEndpoint> eps = Endpoints(Ep("a", "GET", "/users/{id}"));
                        Check.Equal("5", Find(eps, "GET", "/users/5")!.Parameters["id"], "parameter captured");
                        Check.True(Find(eps, "GET", "/users/5/orders") == null, "extra segment does not match");
                        Check.True(Find(eps, "GET", "/users") == null, "missing segment does not match");
                    }),

                    // ---- Precedence ----
                    Case("SpecificRouteBeatsEarlierCatchAll", "A route without a catch-all wins even when a catch-all endpoint comes first", () =>
                    {
                        List<ApiEndpoint> eps = Endpoints(Ep("catch", "GET", "/api/{*rest}"), Ep("users", "GET", "/api/users/{id}"));
                        Check.Equal("users", Find(eps, "GET", "/api/users/7")!.Endpoint.Identifier, "specific route wins");
                        Check.Equal("catch", Find(eps, "GET", "/api/orders/7")!.Endpoint.Identifier, "catch-all serves the rest");
                    }),

                    Case("SpecificRouteBeatsCatchAllInSameEndpoint", "Within one endpoint a later specific route still beats an earlier catch-all", () =>
                    {
                        ApiEndpoint ep = Ep("one", "GET", "/api/{*rest}");
                        ep.Unauthenticated.ParameterizedUrls["GET"].Add("/api/health");
                        MatchingApiEndpoint? m = Find(Endpoints(ep), "GET", "/api/health");
                        Check.Equal("/api/health", m!.ParameterizedUrl, "specific pattern selected");
                    }),

                    Case("AuthenticatedSpecificBeatsUnauthenticatedCatchAll", "A specific authenticated route beats an unauthenticated catch-all", () =>
                    {
                        ApiEndpoint ep = Ep("mixed", "GET", "/api/{*rest}");
                        ep.Authenticated.ParameterizedUrls["GET"] = new List<string> { "/api/admin" };
                        MatchingApiEndpoint? m = Find(Endpoints(ep), "GET", "/api/admin");
                        Check.True(m!.AuthRequired, "authenticated specific route selected");
                    }),

                    Case("LongestPrefixCatchAllWins", "Among catch-alls the longest literal prefix wins regardless of order", () =>
                    {
                        List<ApiEndpoint> eps = Endpoints(Ep("root", "GET", "/{*p}"), Ep("api", "GET", "/api/{*r}"), Ep("v2", "GET", "/api/v2/{*r}"));
                        Check.Equal("v2", Find(eps, "GET", "/api/v2/x/y")!.Endpoint.Identifier, "most specific");
                        Check.Equal("api", Find(eps, "GET", "/api/v1/x")!.Endpoint.Identifier, "next most specific");
                        Check.Equal("root", Find(eps, "GET", "/other")!.Endpoint.Identifier, "root fallback");

                        List<ApiEndpoint> reversed = Endpoints(Ep("v2", "GET", "/api/v2/{*r}"), Ep("api", "GET", "/api/{*r}"), Ep("root", "GET", "/{*p}"));
                        Check.Equal("v2", Find(reversed, "GET", "/api/v2/x/y")!.Endpoint.Identifier, "order does not matter");
                        Check.Equal("root", Find(reversed, "GET", "/")!.Endpoint.Identifier, "root still reachable");
                    }),

                    Case("MoreFixedSegmentsWinsOnTie", "With equal literal prefixes, the catch-all with more fixed segments wins", () =>
                    {
                        List<ApiEndpoint> eps = Endpoints(Ep("short", "GET", "/api/{*r}"), Ep("long", "GET", "/api/{tenant}/{*r}"));
                        Check.Equal("long", Find(eps, "GET", "/api/acme/x")!.Endpoint.Identifier, "more fixed segments");
                        Check.Equal("short", Find(eps, "GET", "/api")!.Endpoint.Identifier, "shorter serves what the longer cannot");
                    }),

                    Case("EqualCatchAllsFirstWins", "Equally specific catch-alls resolve in configuration order", () =>
                    {
                        List<ApiEndpoint> eps = Endpoints(Ep("first", "GET", "/api/{*r}"), Ep("second", "GET", "/api/{*r}"));
                        Check.Equal("first", Find(eps, "GET", "/api/x")!.Endpoint.Identifier, "first configured wins");
                    }),

                    Case("NoMatchReturnsNull", "No route and no catch-all matching returns null", () =>
                    {
                        Check.True(Find(Endpoints(Ep("a", "GET", "/api/{*r}")), "GET", "/nope") == null, "null");
                    }),

                    Case("GuardClauses", "Null endpoints, empty method, or empty URL return null", () =>
                    {
                        Check.True(RouteMatcher.FindEndpoint(null!, "GET", "/x") == null, "null endpoints");
                        Check.True(RouteMatcher.FindEndpoint(Endpoints(Ep("a", "GET", "/{*p}")), "", "/x") == null, "empty method");
                        Check.True(RouteMatcher.FindEndpoint(Endpoints(Ep("a", "GET", "/{*p}")), "GET", "") == null, "empty url");
                        Check.True(RouteMatcher.FindEndpoint(new List<ApiEndpoint> { null! }, "GET", "/x") == null, "null endpoint entry skipped");
                    }),

                    // ---- Invalid patterns ----
                    Case("InvalidPatternNeverMatches", "An invalid pattern is skipped instead of throwing", () =>
                    {
                        Check.True(Find(Endpoints(Ep("bad", "GET", "/{*rest}/edit")), "GET", "/a/edit") == null, "no match, no exception");
                    }),

                    Case("InvalidPatternDoesNotBlockOthers", "Routes after an invalid pattern still match", () =>
                    {
                        ApiEndpoint bad = Ep("bad", "GET", "/{*a}/{*b}");
                        bad.Unauthenticated.ParameterizedUrls["GET"].Add("/ok/{id}");
                        List<ApiEndpoint> eps = Endpoints(bad, Ep("fallback", "GET", "/{*p}"));
                        Check.Equal("bad", Find(eps, "GET", "/ok/1")!.Endpoint.Identifier, "valid route in the same endpoint");
                        Check.Equal("fallback", Find(eps, "GET", "/x/y")!.Endpoint.Identifier, "valid catch-all in another endpoint");
                    }),

                    Case("FindInvalidPatternsReportsRoutesAndRewrites", "FindInvalidPatterns lists every invalid route and rewrite source", () =>
                    {
                        ApiEndpoint ep = Ep("cfg", "GET", "/ok/{*r}");
                        ep.Unauthenticated.ParameterizedUrls["GET"].Add("/{*r}/bad");
                        ep.Authenticated.ParameterizedUrls["POST"] = new List<string> { "/p/{*a}/{*b}" };
                        ep.RewriteUrls[""] = new Dictionary<string, string> { { "/x{*r}", "/y" }, { "/fine/{*r}", "/z/{r}" } };

                        List<string> messages = RouteMatcher.FindInvalidPatterns(Endpoints(ep));
                        Check.Equal(3, messages.Count, "three invalid patterns");
                        Check.True(messages.Any(m => m.Contains("unauthenticated route GET '/{*r}/bad'")), "unauthenticated route reported");
                        Check.True(messages.Any(m => m.Contains("authenticated route POST '/p/{*a}/{*b}'")), "authenticated route reported");
                        Check.True(messages.Any(m => m.Contains("any-method rewrite source '/x{*r}'")), "rewrite reported");
                        Check.True(messages.All(m => m.Contains("endpoint 'cfg'")), "endpoint named");
                    }),

                    Case("FindInvalidPatternsEmptyForValid", "FindInvalidPatterns returns an empty list for valid or missing configuration", () =>
                    {
                        Check.Equal(0, RouteMatcher.FindInvalidPatterns(Endpoints(Ep("a", "GET", "/api/{*r}"))).Count, "valid config");
                        Check.Equal(0, RouteMatcher.FindInvalidPatterns(null!).Count, "null endpoints");
                    }),

                    // ---- URL rewrites ----
                    Case("RewriteCatchAll", "A catch-all rewrite carries the raw remainder to the target", () =>
                    {
                        ApiEndpoint ep = Rewrites("GET", "/legacy/{*rest}", "/v2/{rest}");
                        Check.Equal("/v2/a/b", UrlTools.RewriteUrl("GET", "/legacy/a/b", ep), "rewritten");
                        Check.Equal("/v2/a/b/", UrlTools.RewriteUrl("GET", "/legacy/a/b/", ep), "trailing slash kept");
                        Check.Equal("/v2/", UrlTools.RewriteUrl("GET", "/legacy", ep), "empty remainder");
                    }),

                    Case("RewriteStarPlaceholder", "A target may reference the catch-all as {*name}", () =>
                    {
                        ApiEndpoint ep = Rewrites("GET", "/legacy/{*rest}", "/v2/{*rest}");
                        Check.Equal("/v2/x/y", UrlTools.RewriteUrl("GET", "/legacy/x/y", ep), "rewritten");
                    }),

                    Case("RewriteSpecificBeatsCatchAll", "A rewrite without a catch-all wins over an earlier catch-all rewrite", () =>
                    {
                        ApiEndpoint ep = new ApiEndpoint { Identifier = "rw" };
                        ep.RewriteUrls["GET"] = new Dictionary<string, string>
                        {
                            { "/legacy/{*rest}", "/v2/{rest}" },
                            { "/legacy/special", "/special" }
                        };
                        Check.Equal("/special", UrlTools.RewriteUrl("GET", "/legacy/special", ep), "specific rewrite");
                        Check.Equal("/v2/other", UrlTools.RewriteUrl("GET", "/legacy/other", ep), "catch-all rewrite");
                    }),

                    Case("RewriteMostSpecificCatchAll", "Among catch-all rewrites the most specific wins", () =>
                    {
                        ApiEndpoint ep = new ApiEndpoint { Identifier = "rw" };
                        ep.RewriteUrls["GET"] = new Dictionary<string, string>
                        {
                            { "/{*rest}", "/root/{rest}" },
                            { "/api/{*rest}", "/api-v2/{rest}" }
                        };
                        Check.Equal("/api-v2/x", UrlTools.RewriteUrl("GET", "/api/x", ep), "longer prefix");
                        Check.Equal("/root/x", UrlTools.RewriteUrl("GET", "/x", ep), "root fallback");
                    }),

                    Case("RewriteMethodSpecificBeforeAnyMethod", "Method-specific rewrites still take precedence over any-method catch-alls", () =>
                    {
                        ApiEndpoint ep = new ApiEndpoint { Identifier = "rw" };
                        ep.RewriteUrls["GET"] = new Dictionary<string, string> { { "/a/{*r}", "/get/{r}" } };
                        ep.RewriteUrls[""] = new Dictionary<string, string> { { "/a/{*r}", "/any/{r}" } };
                        Check.Equal("/get/x", UrlTools.RewriteUrl("GET", "/a/x", ep), "GET uses the method-specific rule");
                        Check.Equal("/any/x", UrlTools.RewriteUrl("POST", "/a/x", ep), "POST uses the any-method rule");
                    }),

                    Case("RewriteInvalidSourceIgnored", "An invalid rewrite source is ignored and the URL is unchanged", () =>
                    {
                        ApiEndpoint ep = Rewrites("GET", "/legacy/{*rest}/x", "/v2/{rest}");
                        Check.Equal("/legacy/a/x", UrlTools.RewriteUrl("GET", "/legacy/a/x", ep), "unchanged");
                    }),

                    Case("RewriteNoMatchUnchanged", "A URL no rewrite matches is returned unchanged", () =>
                    {
                        ApiEndpoint ep = Rewrites("GET", "/legacy/{*rest}", "/v2/{rest}");
                        Check.Equal("/current/a", UrlTools.RewriteUrl("GET", "/current/a", ep), "unchanged");
                    }),

                    Case("TrySelectRewriteGuards", "TrySelectRewrite rejects a null matcher and handles null rules", () =>
                    {
                        Check.Throws<ArgumentNullException>(() => RouteMatcher.TrySelectRewrite(null!, new Dictionary<string, string>(), out _, out _), "null matcher");
                        Check.False(RouteMatcher.TrySelectRewrite(new Matcher("/a"), null!, out string target, out NameValueCollection values), "null rules");
                        Check.True(target == null, "no target");
                        Check.Equal(0, values.Count, "empty values");
                    }),

                    // ---- OpenAPI ----
                    Case("OpenApiDocumentsCatchAllAsParameter", "The gateway OpenAPI document lists /api/{*rest} as /api/{rest} and omits invalid patterns", () =>
                    {
                        SwitchboardSettings settings = new SwitchboardSettings();
                        ApiEndpoint ep = Ep("docs", "GET", "/files/{*path}");
                        ep.Unauthenticated.ParameterizedUrls["GET"].Add("/bad/{*x}/y");
                        settings.Endpoints.Add(ep);

                        string json = new SwitchboardOpenApiDocumentGenerator().Generate(settings);
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            JsonElement paths = doc.RootElement.GetProperty("paths");
                            Check.True(paths.TryGetProperty("/files/{path}", out JsonElement item), "catch-all documented as a path parameter");
                            JsonElement parameter = item.GetProperty("get").GetProperty("parameters")[0];
                            Check.Equal("path", parameter.GetProperty("name").GetString(), "parameter name without the asterisk");
                            Check.False(json.Contains("{*"), "no catch-all syntax leaks into the document");
                            Check.False(json.Contains("/bad/"), "invalid pattern omitted");
                        }
                    })
                });
        }

        private static MatchingApiEndpoint? Find(List<ApiEndpoint> endpoints, string method, string url)
        {
            return RouteMatcher.FindEndpoint(endpoints, method, url);
        }

        private static List<ApiEndpoint> Endpoints(params ApiEndpoint[] endpoints)
        {
            return endpoints.ToList();
        }

        private static ApiEndpoint Ep(string identifier, string method, string pattern)
        {
            ApiEndpoint ep = new ApiEndpoint { Identifier = identifier, Name = identifier };
            ep.Unauthenticated.ParameterizedUrls[method] = new List<string> { pattern };
            return ep;
        }

        private static ApiEndpoint Rewrites(string method, string source, string target)
        {
            ApiEndpoint ep = new ApiEndpoint { Identifier = "rw" };
            ep.RewriteUrls[method] = new Dictionary<string, string> { { source, target } };
            return ep;
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Action body)
        {
            return new TestCaseDescriptor(
                suiteId: SuiteId,
                caseId: caseId,
                displayName: displayName,
                executeAsync: _ =>
                {
                    body();
                    return Task.CompletedTask;
                });
        }
    }
}
