using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.Hanimeta.DLsiteScraper.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Hanimeta.DLsiteScraper;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="paths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="serializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths paths, IXmlSerializer serializer)
        : base(paths, serializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    public static PluginConfiguration PluginConfig => Instance?.Configuration ?? new PluginConfiguration();

    /// <inheritdoc />
    public override string Name => "DLsite Scraper";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("d2e313b1-0c08-4a4b-9696-768a06561c3f");

    /// <inheritdoc />
    public override string Description => "DLsite metadata provider with backend scraper service";

    /// <inheritdoc />
    public override string ConfigurationFileName => "Jellyfin.Plugin.Hanimeta.DLsiteScraper.xml";

    /// <summary>
    /// Gets the backend URL with proper formatting.
    /// </summary>
    /// <returns>The formatted backend URL.</returns>
    public static string GetBackendUrl()
    {
        var config = PluginConfig;
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
        var config = PluginConfig;
        return string.IsNullOrWhiteSpace(config.ApiToken) ? null : config.ApiToken.Trim();
    }

    /// <summary>
    /// Gets whether logging is enabled.
    /// </summary>
    /// <returns>True if logging is enabled.</returns>
    public static bool IsLoggingEnabled()
    {
        return PluginConfig.EnableLogging;
    }

    /// <summary>
    /// Validates the current configuration.
    /// </summary>
    /// <returns>True if the configuration is valid.</returns>
    public static bool IsConfigurationValid()
    {
        var backendUrl = GetBackendUrl();
        if (!Uri.TryCreate(backendUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            }
        };
    }

    /// <summary>
    /// Normalize and persist configuration when updated from the Dashboard.
    /// </summary>
    /// <param name="configuration">Incoming configuration.</param>
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        // Normalize values (trim, remove trailing '/') and persist
        if (configuration is PluginConfiguration cfg)
        {
            cfg.BackendUrl = string.IsNullOrWhiteSpace(cfg.BackendUrl)
                ? "http://127.0.0.1:8585"
                : cfg.BackendUrl.Trim().TrimEnd('/');

            cfg.ApiToken = string.IsNullOrWhiteSpace(cfg.ApiToken)
                ? null
                : cfg.ApiToken.Trim();
        }

        base.UpdateConfiguration(configuration);
        SaveConfiguration();
    }
}
