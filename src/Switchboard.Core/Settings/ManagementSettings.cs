#nullable enable

using Switchboard;

namespace Switchboard.Core.Settings
{
    using System;

    /// <summary>
    /// Settings for the Management API.
    /// </summary>
    public class ManagementSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable the Management API.
        /// Default is true.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Base path for the Management API.
        /// Default is /_sb/v1.0/.
        /// </summary>
        public string BasePath
        {
            get => _BasePath;
            set
            {
                if (String.IsNullOrWhiteSpace(value)) value = "/_sb/v1.0/";
                if (!value.EndsWith("/")) value += "/";
                if (!value.StartsWith("/")) value = "/" + value;
                _BasePath = value;
            }
        }

        /// <summary>
        /// Admin bearer token for authentication.
        /// Default is "sbadmin".
        /// </summary>
        public string? AdminToken { get; set; } = "sbadmin";

        /// <summary>
        /// True to require authentication for all management endpoints.
        /// Default is true.
        /// </summary>
        public bool RequireAuthentication { get; set; } = true;

        #endregion

        #region Private-Members

        private string _BasePath = "/_sb/v1.0/";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ManagementSettings()
        {
        }

        #endregion
    }
}
