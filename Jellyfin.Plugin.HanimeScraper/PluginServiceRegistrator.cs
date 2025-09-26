using Jellyfin.Plugin.HanimeScraper.ExternalIds;
using Jellyfin.Plugin.HanimeScraper.Providers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.HanimeScraper;

/// <summary>
/// Plugin service registrator.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Register providers
        serviceCollection.AddSingleton<HanimeMetadataProvider>();
        serviceCollection.AddSingleton<HanimeImageProvider>();

        // Register external ID for movies
        serviceCollection.AddSingleton<MovieId>();
    }
}
