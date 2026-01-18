#nullable enable

namespace Switchboard.Core.Models
{
    using System;

    /// <summary>
    /// Mapping between an API endpoint and an origin server.
    /// Many-to-many relationship table.
    /// </summary>
    public class EndpointOriginMapping
    {
        #region Public-Members

        /// <summary>
        /// Auto-incremented primary key.
        /// </summary>
        public int Id { get; set; } = 0;

        /// <summary>
        /// Endpoint identifier (foreign key).
        /// </summary>
        public string EndpointIdentifier
        {
            get => _EndpointIdentifier;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(EndpointIdentifier));
                _EndpointIdentifier = value;
            }
        }

        /// <summary>
        /// Origin server identifier (foreign key).
        /// </summary>
        public string OriginIdentifier
        {
            get => _OriginIdentifier;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(OriginIdentifier));
                _OriginIdentifier = value;
            }
        }

        /// <summary>
        /// Sort order for load balancing priority.
        /// Lower values are considered first in round-robin.
        /// </summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Timestamp when this record was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Endpoint GUID (foreign key).
        /// </summary>
        public Guid EndpointGUID { get; set; } = Guid.Empty;

        /// <summary>
        /// Origin server GUID (foreign key).
        /// </summary>
        public Guid OriginGUID { get; set; } = Guid.Empty;

        #endregion

        #region Private-Members

        private string _EndpointIdentifier = string.Empty;
        private string _OriginIdentifier = string.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EndpointOriginMapping()
        {
        }

        /// <summary>
        /// Instantiate with parameters.
        /// </summary>
        /// <param name="endpointIdentifier">Endpoint identifier.</param>
        /// <param name="originIdentifier">Origin server identifier.</param>
        /// <param name="sortOrder">Sort order.</param>
        public EndpointOriginMapping(string endpointIdentifier, string originIdentifier, int sortOrder = 0)
        {
            EndpointIdentifier = endpointIdentifier ?? throw new ArgumentNullException(nameof(endpointIdentifier));
            OriginIdentifier = originIdentifier ?? throw new ArgumentNullException(nameof(originIdentifier));
            SortOrder = sortOrder;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
