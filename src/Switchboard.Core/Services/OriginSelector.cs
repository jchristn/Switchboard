#nullable enable

namespace Switchboard.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text;
    using Switchboard.Core.Models;

    /// <summary>
    /// Selects an origin server for a request according to an endpoint's load-balancing configuration.
    /// The selection is a layered pipeline, applied in order: (1) explicit canary header match, (2)
    /// availability filtering (healthy, not passively ejected, not drained, not already tried this
    /// request), (3) priority tiering (lowest priority number wins; higher tiers are backups), (4) sticky
    /// session affinity via consistent hashing when enabled, and finally (5) the base load-balancing mode.
    /// The class is pure and side-effect free apart from advancing the endpoint's round-robin cursor;
    /// callers hold <c>endpoint.Lock</c> around a call so that cursor advance is serialized.
    /// </summary>
    public static class OriginSelector
    {
        #region Public-Members

        /// <summary>
        /// Minimum effective-weight fraction applied during slow start so a warming origin still receives
        /// some traffic rather than none. Default is 0.1. Minimum is 0.0. Maximum is 1.0. Values are clamped.
        /// </summary>
        public static double SlowStartFloor
        {
            get => _SlowStartFloor;
            set
            {
                if (value < 0.0) value = 0.0;
                if (value > 1.0) value = 1.0;
                _SlowStartFloor = value;
            }
        }

        #endregion

        #region Private-Members

        private static double _SlowStartFloor = 0.1;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Select an origin server for a request, or null when no origin is available.
        /// </summary>
        /// <param name="endpoint">Endpoint whose origins and load-balancing configuration are used. Cannot be null.</param>
        /// <param name="allOrigins">All configured runtime origins; the endpoint's origins are resolved from this list. Cannot be null.</param>
        /// <param name="clientIp">Client IP address used as the default sticky-session key. May be null.</param>
        /// <param name="headers">Request headers used for canary matching and header-based stickiness. May be null.</param>
        /// <param name="exclude">Origin identifiers to exclude (for example, origins already tried during retries). May be null.</param>
        /// <param name="rng">Random source for random/weighted/two-choice modes. Cannot be null.</param>
        /// <param name="nowUtc">Current UTC time, used for slow-start and ejection calculations.</param>
        /// <returns>The selected origin, or null when none is available.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="endpoint"/>, <paramref name="allOrigins"/>, or <paramref name="rng"/> is null.</exception>
        public static OriginServer? Select(
            ApiEndpoint endpoint,
            List<OriginServer> allOrigins,
            string? clientIp,
            NameValueCollection? headers,
            ISet<string>? exclude,
            Random rng,
            DateTime nowUtc)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (allOrigins == null) throw new ArgumentNullException(nameof(allOrigins));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            Dictionary<string, OriginServer> originsById = new Dictionary<string, OriginServer>(StringComparer.Ordinal);
            foreach (OriginServer origin in allOrigins)
                if (origin != null && !String.IsNullOrEmpty(origin.Identifier)) originsById[origin.Identifier] = origin;

            List<OriginBinding> bindings = ResolveBindings(endpoint);

            // Restrict to bindings that resolve to a known origin and are not explicitly excluded.
            List<OriginBinding> candidates = new List<OriginBinding>();
            foreach (OriginBinding binding in bindings)
            {
                if (String.IsNullOrEmpty(binding.Identifier)) continue;
                if (exclude != null && exclude.Contains(binding.Identifier)) continue;
                if (!originsById.ContainsKey(binding.Identifier)) continue;
                candidates.Add(binding);
            }

            // (1) Canary header match: if any candidate declares a header match that the request satisfies,
            // and at least one such origin is available, restrict routing to the matching origins.
            List<OriginBinding> canaryMatches = candidates
                .Where(b => CanaryMatches(b, headers))
                .ToList();
            if (canaryMatches.Count > 0 && canaryMatches.Any(b => IsAvailable(originsById[b.Identifier], b, nowUtc)))
                candidates = canaryMatches;

            // (2) Availability filter.
            List<OriginBinding> available = candidates
                .Where(b => IsAvailable(originsById[b.Identifier], b, nowUtc))
                .ToList();
            if (available.Count == 0) return null;

            // (3) Priority tier: keep only the lowest priority number present.
            int minPriority = available.Min(b => b.Priority);
            List<OriginBinding> tier = available.Where(b => b.Priority == minPriority).ToList();
            if (tier.Count == 1) return originsById[tier[0].Identifier];

            // (4) Sticky session affinity via consistent hashing.
            if (endpoint.StickySessionEnabled)
            {
                string? stickyKey = ResolveStickyKey(endpoint, clientIp, headers);
                if (!String.IsNullOrEmpty(stickyKey))
                {
                    OriginBinding sticky = ConsistentHashPick(tier, originsById, stickyKey, nowUtc);
                    return originsById[sticky.Identifier];
                }
            }

            // (5) Base load-balancing mode.
            OriginBinding chosen = SelectByMode(endpoint, tier, originsById, rng, nowUtc);
            return originsById[chosen.Identifier];
        }

        #endregion

        #region Private-Methods

        private static List<OriginBinding> ResolveBindings(ApiEndpoint endpoint)
        {
            if (endpoint.OriginBindings != null && endpoint.OriginBindings.Count > 0)
                return endpoint.OriginBindings;

            List<OriginBinding> defaults = new List<OriginBinding>();
            if (endpoint.OriginServers != null)
            {
                foreach (string identifier in endpoint.OriginServers)
                    if (!String.IsNullOrEmpty(identifier)) defaults.Add(new OriginBinding(identifier));
            }
            return defaults;
        }

        private static bool CanaryMatches(OriginBinding binding, NameValueCollection? headers)
        {
            if (String.IsNullOrEmpty(binding.CanaryHeader) || binding.CanaryValue == null) return false;
            if (headers == null) return false;
            string? value = headers.Get(binding.CanaryHeader);
            return value != null && String.Equals(value, binding.CanaryValue, StringComparison.Ordinal);
        }

        private static bool IsAvailable(OriginServer origin, OriginBinding binding, DateTime nowUtc)
        {
            if (binding.Weight <= 0) return false;
            lock (origin.Lock)
            {
                if (!origin.Healthy) return false;
                if (origin.EjectedUntilUtc.HasValue && nowUtc < origin.EjectedUntilUtc.Value) return false;
            }
            return true;
        }

        private static string? ResolveStickyKey(ApiEndpoint endpoint, string? clientIp, NameValueCollection? headers)
        {
            if (!String.IsNullOrEmpty(endpoint.StickySessionHeader))
            {
                string? headerValue = headers?.Get(endpoint.StickySessionHeader);
                if (!String.IsNullOrEmpty(headerValue)) return headerValue;
                return null;
            }
            return clientIp;
        }

        // Effective weight = configured weight scaled by the slow-start ramp factor. Never returns less
        // than a small positive value for an available origin so it remains selectable.
        private static double EffectiveWeight(OriginServer origin, OriginBinding binding, DateTime nowUtc)
        {
            double factor = SlowStartFactor(origin, nowUtc);
            double weight = binding.Weight * factor;
            if (weight < 0.0001) weight = 0.0001;
            return weight;
        }

        private static double SlowStartFactor(OriginServer origin, DateTime nowUtc)
        {
            int slowStartMs;
            DateTime? lastHealthy;
            lock (origin.Lock)
            {
                slowStartMs = origin.SlowStartMs;
                lastHealthy = origin.LastHealthyUtc;
            }

            if (slowStartMs <= 0 || !lastHealthy.HasValue) return 1.0;

            double elapsed = (nowUtc - lastHealthy.Value).TotalMilliseconds;
            if (elapsed >= slowStartMs) return 1.0;
            if (elapsed <= 0) return SlowStartFloor;

            double fraction = elapsed / slowStartMs;
            if (fraction < SlowStartFloor) fraction = SlowStartFloor;
            return fraction;
        }

        private static int Load(OriginServer origin)
        {
            return System.Threading.Volatile.Read(ref origin.ActiveRequests)
                + System.Threading.Volatile.Read(ref origin.PendingRequests);
        }

        private static OriginBinding SelectByMode(
            ApiEndpoint endpoint,
            List<OriginBinding> tier,
            Dictionary<string, OriginServer> originsById,
            Random rng,
            DateTime nowUtc)
        {
            switch (endpoint.LoadBalancing)
            {
                case LoadBalancingMode.Random:
                    return tier[rng.Next(0, tier.Count)];

                case LoadBalancingMode.RoundRobin:
                    int index = endpoint.RoundRobinIndex % tier.Count;
                    if (index < 0) index = 0;
                    endpoint.RoundRobinIndex = index + 1;
                    return tier[index];

                case LoadBalancingMode.Weighted:
                    return WeightedPick(tier, originsById, rng.NextDouble(), nowUtc);

                case LoadBalancingMode.LeastConnections:
                    return LeastLoadedPick(tier, originsById, nowUtc);

                case LoadBalancingMode.PowerOfTwoChoices:
                    return PowerOfTwoPick(tier, originsById, rng, nowUtc);

                case LoadBalancingMode.LatencyBased:
                    return LowestLatencyPick(tier, originsById, rng, nowUtc);

                default:
                    return tier[rng.Next(0, tier.Count)];
            }
        }

        // Pick using a draw in [0,1): scale by total effective weight and walk the cumulative distribution.
        private static OriginBinding WeightedPick(
            List<OriginBinding> tier,
            Dictionary<string, OriginServer> originsById,
            double draw,
            DateTime nowUtc)
        {
            double total = 0.0;
            foreach (OriginBinding binding in tier)
                total += EffectiveWeight(originsById[binding.Identifier], binding, nowUtc);

            double target = draw * total;
            double cumulative = 0.0;
            foreach (OriginBinding binding in tier)
            {
                cumulative += EffectiveWeight(originsById[binding.Identifier], binding, nowUtc);
                if (target < cumulative) return binding;
            }
            return tier[tier.Count - 1];
        }

        private static OriginBinding LeastLoadedPick(
            List<OriginBinding> tier,
            Dictionary<string, OriginServer> originsById,
            DateTime nowUtc)
        {
            OriginBinding best = tier[0];
            double bestScore = Load(originsById[best.Identifier]) / EffectiveWeight(originsById[best.Identifier], best, nowUtc);
            for (int i = 1; i < tier.Count; i++)
            {
                OriginServer origin = originsById[tier[i].Identifier];
                double score = Load(origin) / EffectiveWeight(origin, tier[i], nowUtc);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = tier[i];
                }
            }
            return best;
        }

        private static OriginBinding PowerOfTwoPick(
            List<OriginBinding> tier,
            Dictionary<string, OriginServer> originsById,
            Random rng,
            DateTime nowUtc)
        {
            if (tier.Count == 1) return tier[0];

            int first = rng.Next(0, tier.Count);
            int second = rng.Next(0, tier.Count - 1);
            if (second >= first) second++;

            OriginBinding a = tier[first];
            OriginBinding b = tier[second];
            double scoreA = Load(originsById[a.Identifier]) / EffectiveWeight(originsById[a.Identifier], a, nowUtc);
            double scoreB = Load(originsById[b.Identifier]) / EffectiveWeight(originsById[b.Identifier], b, nowUtc);
            return scoreA <= scoreB ? a : b;
        }

        // Prefer origins with no latency sample yet (so they gather data); otherwise pick the lowest EWMA.
        private static OriginBinding LowestLatencyPick(
            List<OriginBinding> tier,
            Dictionary<string, OriginServer> originsById,
            Random rng,
            DateTime nowUtc)
        {
            List<OriginBinding> unsampled = new List<OriginBinding>();
            foreach (OriginBinding binding in tier)
            {
                OriginServer origin = originsById[binding.Identifier];
                bool hasSample;
                lock (origin.Lock) { hasSample = origin.HasLatencySample; }
                if (!hasSample) unsampled.Add(binding);
            }
            if (unsampled.Count > 0) return unsampled[rng.Next(0, unsampled.Count)];

            OriginBinding best = tier[0];
            double bestLatency = ReadEwma(originsById[best.Identifier]);
            for (int i = 1; i < tier.Count; i++)
            {
                double latency = ReadEwma(originsById[tier[i].Identifier]);
                if (latency < bestLatency)
                {
                    bestLatency = latency;
                    best = tier[i];
                }
            }
            return best;
        }

        private static double ReadEwma(OriginServer origin)
        {
            lock (origin.Lock) { return origin.EwmaLatencyMs; }
        }

        // Deterministic weight-aware pick from a stable (non-randomized) hash of the sticky key.
        private static OriginBinding ConsistentHashPick(
            List<OriginBinding> tier,
            Dictionary<string, OriginServer> originsById,
            string key,
            DateTime nowUtc)
        {
            double total = 0.0;
            foreach (OriginBinding binding in tier)
                total += EffectiveWeight(originsById[binding.Identifier], binding, nowUtc);

            uint hash = StableHash(key);
            double target = (hash / (double)uint.MaxValue) * total;

            double cumulative = 0.0;
            foreach (OriginBinding binding in tier)
            {
                cumulative += EffectiveWeight(originsById[binding.Identifier], binding, nowUtc);
                if (target < cumulative) return binding;
            }
            return tier[tier.Count - 1];
        }

        // FNV-1a 32-bit hash. Stable across processes (unlike string.GetHashCode) so sticky affinity is
        // consistent for the same key over time.
        private static uint StableHash(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            uint hash = 2166136261;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= 16777619;
            }
            return hash;
        }

        #endregion
    }
}
