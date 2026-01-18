namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Authentication result.
    /// </summary>
    public enum AuthenticationResultEnum
    {
        /// <summary>
        /// Success.
        /// </summary>
        [EnumMember(Value = "Success")]
        Success,
        /// <summary>
        /// InvalidCredentials.
        /// </summary>
        [EnumMember(Value = "InvalidCredentials")]
        InvalidCredentials,
        /// <summary>
        /// NotFound.
        /// </summary>
        [EnumMember(Value = "NotFound")]
        NotFound,
        /// <summary>
        /// Inactive.
        /// </summary>
        [EnumMember(Value = "Inactive")]
        Inactive,
        /// <summary>
        /// Denied.
        /// </summary>
        [EnumMember(Value = "Denied")]
        Denied,
        /// <summary>
        /// Locked.
        /// </summary>
        [EnumMember(Value = "Locked")]
        Locked,
        /// <summary>
        /// TemporaryPassword.
        /// </summary>
        [EnumMember(Value = "TemporaryPassword")]
        TemporaryPassword,
        /// <summary>
        /// PasswordExpired.
        /// </summary>
        [EnumMember(Value = "PasswordExpired")]
        PasswordExpired,
        /// <summary>
        /// TokenExpired.
        /// </summary>
        [EnumMember(Value = "TokenExpired")]
        TokenExpired,
        /// <summary>
        /// SessionExpired.
        /// </summary>
        [EnumMember(Value = "SessionExpired")]
        SessionExpired,
        /// <summary>
        /// DeviceNotTrusted.
        /// </summary>
        [EnumMember(Value = "DeviceNotTrusted")]
        DeviceNotTrusted,
        /// <summary>
        /// LocationBlocked.
        /// </summary>
        [EnumMember(Value = "LocationBlocked")]
        LocationBlocked,
        /// <summary>
        /// InternalError.
        /// </summary>
        [EnumMember(Value = "InternalError")]
        InternalError
    }
}
