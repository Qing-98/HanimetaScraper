using System;

namespace Jellyfin.Plugin.HanimeScraper.Configuration;

/// <summary>
/// Configuration manager for Hanime Scraper plugin.
/// </summary>
public static class ConfigurationManager
{
    /// <summary>
    /// Gets the current plugin configuration with fallback defaults.
    /// </summary>
    /// <returns>The plugin configuration.</returns>
    public static PluginConfiguration GetConfiguration()
    {
        return Plugin.Instance?.Configuration ?? new PluginConfiguration();
    }

    /// <summary>
    /// Gets the backend URL with proper formatting.
    /// </summary>
    /// <returns>The formatted backend URL.</returns>
    public static string GetBackendUrl()
    {
        var config = GetConfiguration();
        return string.IsNullOrWhiteSpace(config.BackendUrl)
            ? "http://127.0.0.1:8585"
            : config.BackendUrl.TrimEnd('/');
    }

    /// <summary>
    /// Gets the API token if configured.
    /// </summary>
    /// <returns>The API token or null if not configured.</returns>
    public static string? GetApiToken()
    {
        var config = GetConfiguration();
        return string.IsNullOrWhiteSpace(config.ApiToken) ? null : config.ApiToken.Trim();
    }

    /// <summary>
    /// Gets whether logging is enabled.
    /// </summary>
    /// <returns>True if logging is enabled.</returns>
    public static bool IsLoggingEnabled()
    {
        return GetConfiguration().EnableLogging;
    }

    /// <summary>
    /// Validates the current configuration.
    /// </summary>
    /// <returns>True if the configuration is valid.</returns>
    public static bool IsConfigurationValid()
    {
        var config = GetConfiguration();

        // Check if backend URL is properly formatted
        var backendUrl = GetBackendUrl();
        if (!Uri.TryCreate(backendUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return false;
        }

        return true;
    }
}
