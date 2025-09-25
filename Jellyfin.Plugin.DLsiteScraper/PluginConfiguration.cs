using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DLsiteScraper;

/// <summary>
/// Plugin configuration for DLsite Scraper.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
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
    public bool EnableLogging { get; set; } = true;
}
