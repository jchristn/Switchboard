#nullable enable

namespace Switchboard.Core.Models
{
    using System;

    /// <summary>
    /// The per-endpoint view of an origin server, projected from an endpoint-origin mapping. Carries the
    /// routing weight, priority tier, and optional canary header match that apply to this origin only when
    /// it is used by a specific endpoint. Weight and priority are per-mapping so the same origin can play
    /// different roles across endpoints (for example, a canary target for one endpoint and a full-weight
    /// backend for another).
    /// </summary>
    public class OriginBinding
    {
        #region Public-Members

        /// <summary>
        /// Identifier of the bound origin server.
        /// </summary>
        public string Identifier
        {
            get => _Identifier;
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Identifier));
                _Identifier = value;
            }
        }

        /// <summary>
        /// Relative routing weight for this origin within the endpoint. Higher weights receive
        /// proportionally more traffic in weight-aware modes. A weight of 0 drains the origin: it is still
        /// health-checked but never selected. Default is 100. Minimum is 0. Maximum is 10000.
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
        /// lowest priority tier present among available origins is used, and higher-numbered tiers act as
        /// backups that receive traffic only when the whole lower tier is unavailable. Default is 0.
        /// Minimum is 0. Maximum is 1000.
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
        /// forces the request to this origin (explicit canary/blue-green targeting). Null disables the
        /// header match for this binding.
        /// </summary>
        public string? CanaryHeader { get; set; } = null;

        /// <summary>
        /// Required value of <see cref="CanaryHeader"/> for the canary match to apply. Null disables the
        /// header match for this binding.
        /// </summary>
        public string? CanaryValue { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Identifier = null!;
        private int _Weight = 100;
        private int _Priority = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OriginBinding()
        {
        }

        /// <summary>
        /// Instantiate with an identifier.
        /// </summary>
        /// <param name="identifier">Origin server identifier. Cannot be null or empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="identifier"/> is null or empty.</exception>
        public OriginBinding(string identifier)
        {
            Identifier = identifier;
        }

        #endregion
    }
}
