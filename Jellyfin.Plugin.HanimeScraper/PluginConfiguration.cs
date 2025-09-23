using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.HanimeScraper;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the backend URL.
    /// </summary>
    public string BackendUrl { get; set; } = "http://127.0.0.1:8585";

    /// <summary>
    /// Gets or sets the API token.
    /// </summary>
    public string? ApiToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether logging is enabled.
    /// </summary>
    public bool EnableLogging { get; set; } = true;
}
