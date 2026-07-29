namespace LoadGenerator
{
    /// <summary>
    /// Template describing a single route (HTTP method plus URL pattern) on an endpoint. The URL
    /// pattern may contain <c>{id}</c>-style parameters, which are replaced with random values when
    /// concrete request paths are generated.
    /// </summary>
    public sealed class RouteSpec
    {
        #region Public-Members

        /// <summary>
        /// HTTP method (for example GET or POST).
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// URL pattern, optionally containing parameters such as <c>/api/users/{id}</c>.
        /// </summary>
        public string UrlPattern { get; }

        /// <summary>
        /// Relative weight controlling how frequently this route is chosen among its endpoint's
        /// routes. Minimum meaningful value is 1.
        /// </summary>
        public int Weight { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize a new route template.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="urlPattern">URL pattern, optionally parameterized.</param>
        /// <param name="weight">Relative selection weight (minimum 1).</param>
        public RouteSpec(string method, string urlPattern, int weight)
        {
            Method = method;
            UrlPattern = urlPattern;
            Weight = weight < 1 ? 1 : weight;
        }

        #endregion
    }
}
