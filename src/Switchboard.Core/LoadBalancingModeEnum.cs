using System.Runtime.Serialization;

namespace Switchboard.Core
{
    /// <summary>
    /// Load balancing mode.
    /// </summary>
    public enum LoadBalancingMode
    {
        /// <summary>
        /// Random.
        /// </summary>
        [EnumMember(Value = "Random")]
        Random,
        /// <summary>
        /// Round robin.
        /// </summary>
        [EnumMember(Value = "RoundRobin")]
        RoundRobin
    }
}
