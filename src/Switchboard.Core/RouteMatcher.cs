namespace Switchboard.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using UrlMatcher;

    /// <summary>
    /// Parses route and rewrite patterns and matches requests to API endpoints.
    /// Patterns use {name} for a single-segment parameter and {*name} as the entire final segment for a
    /// catch-all that matches zero or more remaining segments, for example /api/{*rest}.
    /// When several routes match a request, a route without a catch-all always wins, in configuration
    /// order. Catch-all routes are considered only when no other route matches, and the most specific
    /// one wins: the longest literal prefix, then the most fixed segments, then configuration order.
    /// Invalid patterns never match. All members are thread-safe.
    /// </summary>
    public static class RouteMatcher
    {
        #region Public-Members

        /// <summary>
        /// Maximum number of parsed patterns kept in the process-wide cache before it is cleared and
        /// rebuilt on demand. Default is 10000. Minimum is 1. Values below 1 are raised to 1.
        /// </summary>
        public static int MaxCachedPatterns
        {
            get => _MaxCachedPatterns;
            set
            {
                if (value < 1) value = 1;
                _MaxCachedPatterns = value;
            }
        }

        #endregion

        #region Private-Members

        private static int _MaxCachedPatterns = 10000;
        private static readonly ConcurrentDictionary<string, UrlPattern> _ValidPatterns = new ConcurrentDictionary<string, UrlPattern>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, string> _InvalidPatterns = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Parse a route or rewrite pattern, using a process-wide cache so each distinct pattern is
        /// parsed once.
        /// </summary>
        /// <param name="pattern">Pattern, for example /users/{id} or /api/{*rest}.</param>
        /// <param name="parsed">Parsed pattern, or null when the pattern is invalid.</param>
        /// <param name="error">Reason the pattern is invalid, or null when it is valid.</param>
        /// <returns>True if the pattern is valid.</returns>
        public static bool TryParsePattern(string pattern, out UrlPattern parsed, out string error)
        {
            parsed = null;
            error = null;

            if (String.IsNullOrWhiteSpace(pattern))
            {
                error = "URL pattern is required.";
                return false;
            }

            if (_ValidPatterns.TryGetValue(pattern, out parsed)) return true;
            if (_InvalidPatterns.TryGetValue(pattern, out error)) return false;

            TrimCache();

            try
            {
                parsed = new UrlPattern(pattern);
                _ValidPatterns[pattern] = parsed;
                return true;
            }
            catch (ArgumentException e)
            {
                error = CleanMessage(e);
                _InvalidPatterns[pattern] = error;
                return false;
            }
        }

        /// <summary>
        /// Find the API endpoint and route that should serve a request.
        /// </summary>
        /// <param name="endpoints">Configured API endpoints, in configuration order. May be null.</param>
        /// <param name="method">Upper-case HTTP method, for example GET.</param>
        /// <param name="url">Request path, with or without a query string.</param>
        /// <returns>The matching endpoint and route, or null when nothing matches.</returns>
        public static MatchingApiEndpoint FindEndpoint(IEnumerable<ApiEndpoint> endpoints, string method, string url)
        {
            if (endpoints == null) return null;
            if (String.IsNullOrEmpty(method) || String.IsNullOrEmpty(url)) return null;

            Matcher matcher = new Matcher(url);

            MatchingApiEndpoint bestCatchAll = null;
            UrlPattern bestCatchAllPattern = null;

            foreach (ApiEndpoint ep in endpoints)
            {
                if (ep == null) continue;

                for (int g = 0; g < 2; g++)
                {
                    bool authRequired = g == 1;
                    ApiEndpointGroup group = authRequired ? ep.Authenticated : ep.Unauthenticated;
                    if (group == null || group.ParameterizedUrls == null) continue;
                    if (!group.ParameterizedUrls.TryGetValue(method, out List<string> patterns) || patterns == null) continue;

                    foreach (string patternText in patterns)
                    {
                        if (!TryParsePattern(patternText, out UrlPattern pattern, out _)) continue;

                        if (pattern.IsCatchAll)
                        {
                            // only a strictly more specific catch-all can replace one found earlier
                            if (bestCatchAllPattern != null && !IsMoreSpecific(pattern, bestCatchAllPattern)) continue;

                            if (matcher.Match(pattern, out NameValueCollection catchAllValues))
                            {
                                bestCatchAllPattern = pattern;
                                bestCatchAll = new MatchingApiEndpoint
                                {
                                    AuthRequired = authRequired,
                                    Endpoint = ep,
                                    ParameterizedUrl = patternText,
                                    Parameters = catchAllValues
                                };
                            }

                            continue;
                        }

                        if (matcher.Match(pattern, out NameValueCollection values))
                        {
                            return new MatchingApiEndpoint
                            {
                                AuthRequired = authRequired,
                                Endpoint = ep,
                                ParameterizedUrl = patternText,
                                Parameters = values
                            };
                        }
                    }
                }
            }

            return bestCatchAll;
        }

        /// <summary>
        /// Select the rewrite rule that applies to a URL from a set of source-to-target rules. Rules
        /// without a catch-all are tried first in order, then the most specific matching catch-all.
        /// Invalid source patterns are ignored.
        /// </summary>
        /// <param name="matcher">Matcher built from the request URL.</param>
        /// <param name="rules">Rewrite rules, source pattern to target pattern. May be null.</param>
        /// <param name="target">Target pattern of the selected rule, or null when none matches.</param>
        /// <param name="values">Values captured from the URL by the selected rule. Never null.</param>
        /// <returns>True if a rule matched.</returns>
        /// <exception cref="ArgumentNullException">Thrown when matcher is null.</exception>
        public static bool TrySelectRewrite(Matcher matcher, IDictionary<string, string> rules, out string target, out NameValueCollection values)
        {
            if (matcher == null) throw new ArgumentNullException(nameof(matcher));

            target = null;
            values = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
            if (rules == null || rules.Count == 0) return false;

            UrlPattern bestCatchAllPattern = null;
            string bestCatchAllTarget = null;
            NameValueCollection bestCatchAllValues = null;

            foreach (KeyValuePair<string, string> rule in rules)
            {
                if (String.IsNullOrEmpty(rule.Value)) continue;
                if (!TryParsePattern(rule.Key, out UrlPattern pattern, out _)) continue;

                if (pattern.IsCatchAll)
                {
                    if (bestCatchAllPattern != null && !IsMoreSpecific(pattern, bestCatchAllPattern)) continue;

                    if (matcher.Match(pattern, out NameValueCollection catchAllValues))
                    {
                        bestCatchAllPattern = pattern;
                        bestCatchAllTarget = rule.Value;
                        bestCatchAllValues = catchAllValues;
                    }

                    continue;
                }

                if (matcher.Match(pattern, out NameValueCollection matched))
                {
                    target = rule.Value;
                    values = matched;
                    return true;
                }
            }

            if (bestCatchAllPattern == null) return false;

            target = bestCatchAllTarget;
            values = bestCatchAllValues;
            return true;
        }

        /// <summary>
        /// List every invalid route and rewrite source pattern in a set of endpoints. Invalid patterns
        /// never match, so this is used to warn operators when configuration is loaded.
        /// </summary>
        /// <param name="endpoints">API endpoints. May be null.</param>
        /// <returns>One human-readable message per invalid pattern. Never null.</returns>
        public static List<string> FindInvalidPatterns(IEnumerable<ApiEndpoint> endpoints)
        {
            List<string> messages = new List<string>();
            if (endpoints == null) return messages;

            foreach (ApiEndpoint ep in endpoints)
            {
                if (ep == null) continue;

                AddInvalidRoutes(messages, ep, ep.Unauthenticated, "unauthenticated");
                AddInvalidRoutes(messages, ep, ep.Authenticated, "authenticated");

                if (ep.RewriteUrls == null) continue;

                foreach (KeyValuePair<string, Dictionary<string, string>> bucket in ep.RewriteUrls)
                {
                    if (bucket.Value == null) continue;

                    foreach (string source in bucket.Value.Keys)
                    {
                        if (!TryParsePattern(source, out _, out string error))
                        {
                            string method = String.IsNullOrEmpty(bucket.Key) ? "any-method" : bucket.Key;
                            messages.Add("endpoint '" + ep.Identifier + "' " + method + " rewrite source '" + source + "' is invalid and will be ignored: " + error);
                        }
                    }
                }
            }

            return messages;
        }

        #endregion

        #region Private-Methods

        private static void AddInvalidRoutes(List<string> messages, ApiEndpoint ep, ApiEndpointGroup group, string label)
        {
            if (group == null || group.ParameterizedUrls == null) return;

            foreach (KeyValuePair<string, List<string>> bucket in group.ParameterizedUrls)
            {
                if (bucket.Value == null) continue;

                foreach (string pattern in bucket.Value)
                {
                    if (!TryParsePattern(pattern, out _, out string error))
                        messages.Add("endpoint '" + ep.Identifier + "' " + label + " route " + bucket.Key + " '" + pattern + "' is invalid and will be ignored: " + error);
                }
            }
        }

        private static bool IsMoreSpecific(UrlPattern candidate, UrlPattern current)
        {
            if (candidate.LiteralPrefixCount != current.LiteralPrefixCount)
                return candidate.LiteralPrefixCount > current.LiteralPrefixCount;

            if (candidate.FixedSegmentCount != current.FixedSegmentCount)
                return candidate.FixedSegmentCount > current.FixedSegmentCount;

            return candidate.LiteralCount > current.LiteralCount;
        }

        private static string CleanMessage(ArgumentException e)
        {
            string message = e.Message ?? "Invalid URL pattern.";

            // .NET appends " (Parameter 'name')" to ArgumentException messages; it adds nothing for an operator
            if (!String.IsNullOrEmpty(e.ParamName))
            {
                string suffix = " (Parameter '" + e.ParamName + "')";
                if (message.EndsWith(suffix, StringComparison.Ordinal))
                    message = message.Substring(0, message.Length - suffix.Length);
            }

            return message;
        }

        private static void TrimCache()
        {
            if (_ValidPatterns.Count + _InvalidPatterns.Count < _MaxCachedPatterns) return;
            _ValidPatterns.Clear();
            _InvalidPatterns.Clear();
        }

        #endregion
    }
}
