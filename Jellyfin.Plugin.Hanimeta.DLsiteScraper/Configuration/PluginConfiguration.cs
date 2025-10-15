using Jellyfin.Plugin.Hanimeta.Common.Configuration;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Hanimeta.DLsiteScraper.Configuration
{
    /// <summary>
    /// Plugin configuration for DLsite Scraper.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration()
        {
            // Initialize with default values
        }

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
