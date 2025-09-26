using Jellyfin.Plugin.DLsiteScraper.ExternalIds;
using Jellyfin.Plugin.DLsiteScraper.Providers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.DLsiteScraper;

/// <summary>
/// Plugin service registrator.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Register providers
        serviceCollection.AddSingleton<DLsiteMetadataProvider>();
        serviceCollection.AddSingleton<DLsiteImageProvider>();

        // Register external ID for movies
        serviceCollection.AddSingleton<MovieId>();
    }
}
