using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.HanimeScraper.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.HanimeScraper;

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
    public override string Name => "Hanime Scraper";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("18a03d4c-4691-424c-9fda-fe675ea849c4");

    /// <inheritdoc />
    public override string Description => "Hanime metadata provider with backend scraper service";

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
