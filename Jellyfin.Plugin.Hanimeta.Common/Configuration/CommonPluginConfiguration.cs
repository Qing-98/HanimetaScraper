namespace Jellyfin.Plugin.Hanimeta.Common.Configuration
{
    /// <summary>
    /// Configuration options for tag/genre mapping in Jellyfin.
    /// </summary>
    public enum TagMappingMode
    {
        /// <summary>
        /// Map tags to Jellyfin's Tags field.
        /// </summary>
        Tags = 0,

        /// <summary>
        /// Map tags to Jellyfin's Genres field.
        /// </summary>
        Genres = 1,
    }

    /// <summary>
    /// Common configuration settings shared across all scraper plugins.
    /// </summary>
    public class CommonPluginConfiguration
    {
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
