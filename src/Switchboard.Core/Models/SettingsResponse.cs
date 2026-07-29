namespace Switchboard.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    using Switchboard.Core;

    /// <summary>
    /// The settings payload returned by the management API: the full <see cref="SwitchboardSettings"/>
    /// tree (with secrets masked) plus metadata describing which fields require a restart and which
    /// apply at runtime. The metadata arrays are serialized in camelCase and after the configuration
    /// sections, matching the documented response shape.
    /// </summary>
    public sealed class SettingsResponse : SwitchboardSettings
    {
        #region Public-Members

        /// <summary>
        /// Dotted setting paths that only take effect after a restart.
        /// </summary>
        [JsonPropertyName("restartRequiredSettings")]
        [JsonPropertyOrder(1000)]
        public string[] RestartRequiredSettings { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Dotted setting paths that apply immediately at runtime.
        /// </summary>
        [JsonPropertyName("runtimeEditableSettings")]
        [JsonPropertyOrder(1001)]
        public string[] RuntimeEditableSettings { get; set; } = Array.Empty<string>();

        #endregion
    }
}
