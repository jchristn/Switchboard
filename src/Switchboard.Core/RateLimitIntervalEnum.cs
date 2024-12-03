using System.Runtime.Serialization;

namespace Switchboard.Core
{
    /// <summary>
    /// Rate limit interval.
    /// </summary>
    public enum RateLimitIntervalEnum
    {
        /// <summary>
        /// Seconds.
        /// </summary>
        [EnumMember(Value = "Seconds")]
        Seconds,
        /// <summary>
        /// Minutes.
        /// </summary>
        [EnumMember(Value = "Minutes")]
        Minutes,
        /// <summary>
        /// Hours.
        /// </summary>
        [EnumMember(Value = "Hours")]
        Hours,
        /// <summary>
        /// Days.
        /// </summary>
        [EnumMember(Value = "Days")]
        Days
    }
}
