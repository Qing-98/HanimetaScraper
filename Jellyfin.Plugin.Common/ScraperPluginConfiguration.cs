using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Common;

/// <summary>
/// Base plugin configuration shared between Hanime and DLsite scrapers.
/// This eliminates code duplication and ensures consistent configuration across plugins.
/// </summary>
/// <example>
/// Usage in derived plugin configuration:
/// <code>
/// public class MyPluginConfiguration : ScraperPluginConfiguration
/// {
///     // Additional plugin-specific properties can be added here
/// }
/// </code>
///
/// Test cases for configuration:
/// <code>
/// var config = new MyPluginConfiguration();
///
/// // Test default values
/// Assert.Equal("http://127.0.0.1:8585", config.BackendUrl);
/// Assert.Null(config.ApiToken);
/// Assert.True(config.EnableLogging);
///
/// // Test property assignment
/// config.BackendUrl = "http://localhost:9090";
/// config.ApiToken = "test-token";
/// config.EnableLogging = false;
///
/// Assert.Equal("http://localhost:9090", config.BackendUrl);
/// Assert.Equal("test-token", config.ApiToken);
/// Assert.False(config.EnableLogging);
/// </code>
/// </example>
public abstract class ScraperPluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the backend URL for the scraper service.
    /// This should point to the running instance of ScraperBackendService.
    /// </summary>
    /// <example>
    /// Valid URLs:
    /// - "http://127.0.0.1:8585" (default local)
    /// - "http://localhost:8585" (local alternative)
    /// - "https://scraper.example.com" (remote HTTPS)
    /// - "http://192.168.1.100:8585" (LAN deployment)
    /// </example>
    public string BackendUrl { get; set; } = "http://127.0.0.1:8585";

    /// <summary>
    /// Gets or sets the API token for authenticating with the backend service.
    /// This is optional - leave null if the backend service doesn't require authentication.
    /// </summary>
    /// <example>
    /// Example token values:
    /// - null (no authentication)
    /// - "my-secret-token-123"
    /// - "Bearer eyJhbGciOiJIUzI1NiIs..."
    /// - Any custom string configured in the backend service
    /// </example>
    public string? ApiToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether detailed logging is enabled for this plugin.
    /// When enabled, the plugin will log debug information, API calls, and processing details.
    /// Disable in production to reduce log volume.
    /// </summary>
    /// <example>
    /// Logging behavior:
    /// - true: Logs debug, info, warning, and error messages
    /// - false: Only logs critical errors to prevent plugin failures
    /// </example>
    public bool EnableLogging { get; set; } = true;
}
