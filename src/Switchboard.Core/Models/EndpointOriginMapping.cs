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
        /// Sort order for the origin within the endpoint.
        /// Lower values are considered first in round-robin.
        /// </summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Relative routing weight for this origin within the endpoint. Higher weights receive
        /// proportionally more traffic in weight-aware load-balancing modes. A weight of 0 drains the
        /// origin: it is still health-checked but never selected, which is useful for canary rollouts and
        /// graceful draining. Default is 100. Minimum is 0. Maximum is 10000. Values are clamped into range.
        /// </summary>
        public int Weight
        {
            get => _Weight;
            set
            {
                if (value < 0) value = 0;
                if (value > 10000) value = 10000;
                _Weight = value;
            }
        }

        /// <summary>
        /// Priority tier for this origin within the endpoint. Lower numbers are higher priority: only the
        /// lowest priority tier present among available origins receives traffic, and higher-numbered tiers
        /// act as backups used only when the whole lower tier is unavailable. Default is 0. Minimum is 0.
        /// Maximum is 1000. Values are clamped into range.
        /// </summary>
        public int Priority
        {
            get => _Priority;
            set
            {
                if (value < 0) value = 0;
                if (value > 1000) value = 1000;
                _Priority = value;
            }
        }

        /// <summary>
        /// Optional request header name that, when present with the value in <see cref="CanaryValue"/>,
        /// forces matching requests to this origin (explicit canary/blue-green targeting). Null (the
        /// default) disables header-based targeting for this mapping.
        /// </summary>
        public string? CanaryHeader { get; set; } = null;

        /// <summary>
        /// Required value of <see cref="CanaryHeader"/> for the canary header match to apply. Null (the
        /// default) disables header-based targeting for this mapping.
        /// </summary>
        public string? CanaryValue { get; set; } = null;

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
        private int _Weight = 100;
        private int _Priority = 0;

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
