using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Hanimeta.Common.Configuration
{
    /// <summary>
    /// Common plugin configuration class for Hanimeta plugins.
    /// </summary>
    public class HanimetaPluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets the backend URL for the scraper service.
        /// </summary>
        public string BackendUrl { get; set; } = "http://127.0.0.1:8585";

        /// <summary>
        /// Gets or sets the API token for authenticating with the backend service.
        /// </summary>
        public string? ApiToken { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether detailed logging is enabled.
        /// </summary>
        public bool EnableLogging { get; set; } = false;

        /// <summary>
        /// Gets or sets how tags should be mapped in Jellyfin.
        /// </summary>
        /// <value>
        /// TagMappingMode.Tags - Maps tags to Jellyfin's Tags field (default).
        /// TagMappingMode.Genres - Maps tags to Jellyfin's Genres field.
        /// </value>
        public TagMappingMode TagMappingMode { get; set; } = TagMappingMode.Tags;
    }
}
