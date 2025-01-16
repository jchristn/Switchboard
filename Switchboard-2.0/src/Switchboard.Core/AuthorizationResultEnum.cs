namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Authorization result.
    /// </summary>
    public enum AuthorizationResultEnum
    {
        /// <summary>
        /// Success.
        /// </summary>
        [EnumMember(Value = "Success")]
        Success,
        /// <summary>
        /// Denied.
        /// </summary>
        [EnumMember(Value = "Denied")]
        Denied,
        /// <summary>
        /// InternalError.
        /// </summary>
        [EnumMember(Value = "InternalError")]
        InternalError
    }
}
