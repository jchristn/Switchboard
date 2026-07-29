namespace LoadGenerator
{
    /// <summary>
    /// Template describing a synthetic origin server to create in the database.
    /// </summary>
    public sealed class OriginSpec
    {
        #region Public-Members

        /// <summary>
        /// Unique origin identifier.
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// Human-readable display name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Hostname the origin would be reached at.
        /// </summary>
        public string Hostname { get; }

        /// <summary>
        /// TCP port the origin would listen on. Range is 1 to 65535.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Whether the origin would be reached over TLS.
        /// </summary>
        public bool Ssl { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize a new origin template.
        /// </summary>
        /// <param name="identifier">Unique origin identifier.</param>
        /// <param name="name">Display name.</param>
        /// <param name="hostname">Hostname.</param>
        /// <param name="port">TCP port (1 to 65535).</param>
        /// <param name="ssl">Whether TLS is used.</param>
        public OriginSpec(string identifier, string name, string hostname, int port, bool ssl)
        {
            Identifier = identifier;
            Name = name;
            Hostname = hostname;
            Port = port;
            Ssl = ssl;
        }

        #endregion
    }
}
