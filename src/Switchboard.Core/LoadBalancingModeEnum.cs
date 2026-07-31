namespace Switchboard.Core
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Load balancing mode used to select an origin server from the healthy pool for an endpoint.
    /// </summary>
    public enum LoadBalancingMode
    {
        /// <summary>
        /// Select a healthy origin uniformly at random. Weight is ignored.
        /// </summary>
        [EnumMember(Value = "Random")]
        Random,
        /// <summary>
        /// Rotate through the healthy origins in order. Weight is ignored.
        /// </summary>
        [EnumMember(Value = "RoundRobin")]
        RoundRobin,
        /// <summary>
        /// Select the healthy origin with the fewest in-flight requests, normalized by weight
        /// (load divided by effective weight). Spreads load toward less-busy backends.
        /// </summary>
        [EnumMember(Value = "LeastConnections")]
        LeastConnections,
        /// <summary>
        /// Sample two healthy origins at random (weighted) and pick the less-busy of the two
        /// (load divided by effective weight). Approximates least-connections without a full scan
        /// and avoids the herd effect of always choosing the single least-busy origin.
        /// </summary>
        [EnumMember(Value = "PowerOfTwoChoices")]
        PowerOfTwoChoices,
        /// <summary>
        /// Select a healthy origin at random with probability proportional to its effective weight.
        /// </summary>
        [EnumMember(Value = "Weighted")]
        Weighted,
        /// <summary>
        /// Select the healthy origin with the lowest exponentially-weighted moving average (EWMA)
        /// response latency. Origins with no latency samples yet are preferred so they gather data.
        /// </summary>
        [EnumMember(Value = "LatencyBased")]
        LatencyBased
    }
}
