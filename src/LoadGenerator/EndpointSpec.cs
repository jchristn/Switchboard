namespace LoadGenerator
{
    using System.Collections.Generic;

    /// <summary>
    /// Template describing a synthetic API endpoint: its routes, the origins that serve it, and the
    /// relative share of overall traffic it should receive.
    /// </summary>
    public sealed class EndpointSpec
    {
        #region Public-Members

        /// <summary>
        /// Unique endpoint identifier.
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// Human-readable display name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Load balancing mode, either "RoundRobin" or "Random".
        /// </summary>
        public string LoadBalancing { get; }

        /// <summary>
        /// Relative weight controlling this endpoint's share of total traffic. Minimum meaningful
        /// value is 1.
        /// </summary>
        public int Weight { get; }

        /// <summary>
        /// Whether requests to this endpoint are treated as authenticated.
        /// </summary>
        public bool Authenticated { get; }

        /// <summary>
        /// Routes exposed by this endpoint. Never null.
        /// </summary>
        public IReadOnlyList<RouteSpec> Routes { get; }

        /// <summary>
        /// Identifiers of the origins that serve this endpoint. Never null.
        /// </summary>
        public IReadOnlyList<string> OriginIdentifiers { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize a new endpoint template.
        /// </summary>
        /// <param name="identifier">Unique endpoint identifier.</param>
        /// <param name="name">Display name.</param>
        /// <param name="loadBalancing">Load balancing mode ("RoundRobin" or "Random").</param>
        /// <param name="weight">Relative traffic weight (minimum 1).</param>
        /// <param name="authenticated">Whether requests are authenticated.</param>
        /// <param name="routes">Routes exposed by this endpoint.</param>
        /// <param name="originIdentifiers">Identifiers of serving origins.</param>
        public EndpointSpec(
            string identifier,
            string name,
            string loadBalancing,
            int weight,
            bool authenticated,
            IReadOnlyList<RouteSpec> routes,
            IReadOnlyList<string> originIdentifiers)
        {
            Identifier = identifier;
            Name = name;
            LoadBalancing = loadBalancing;
            Weight = weight < 1 ? 1 : weight;
            Authenticated = authenticated;
            Routes = routes ?? new List<RouteSpec>();
            OriginIdentifiers = originIdentifiers ?? new List<string>();
        }

        #endregion
    }
}
