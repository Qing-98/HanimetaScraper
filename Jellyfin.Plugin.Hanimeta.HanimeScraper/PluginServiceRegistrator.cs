using System;
using Jellyfin.Plugin.Hanimeta.Common.Extensions;
using Jellyfin.Plugin.Hanimeta.Common.Registration;
using Jellyfin.Plugin.Hanimeta.HanimeScraper.Client;
using Jellyfin.Plugin.Hanimeta.HanimeScraper.ExternalIds;
using Jellyfin.Plugin.Hanimeta.HanimeScraper.ExternalUrls;
using Jellyfin.Plugin.Hanimeta.HanimeScraper.Providers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.HanimeScraper;

/// <summary>
/// Plugin service registrator.
/// </summary>
public class PluginServiceRegistrator : BasePluginServiceRegistrator, IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Set up logging delegate to use plugin configuration
        LoggingExtensions.IsLoggingEnabled = () => Plugin.IsLoggingEnabled();

        RegisterServices(serviceCollection, () => Plugin.IsLoggingEnabled());
    }

    /// <inheritdoc />
    protected override void RegisterPluginServices(IServiceCollection services)
    {
        // Register HTTP client factory for API calls
        services.AddSingleton<Func<HanimeApiClient>>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<HanimeApiClient>>();
            return () => new HanimeApiClient(logger, Plugin.GetBackendUrl(), Plugin.GetApiToken());
        });

        // Register metadata providers - these need to be registered with the correct interfaces
        services.AddSingleton<IMetadataProvider<Movie>, HanimeMetadataProvider>();
        services.AddSingleton<IRemoteImageProvider, HanimeImageProvider>();

        // Register external ID provider - this needs to be registered with the correct interface
        services.AddSingleton<IExternalId, MovieId>();

        // Register external URL provider so Jellyfin UI can retrieve detail page URLs
        services.AddSingleton<IExternalUrlProvider, HanimeExternalUrlProvider>();
    }
}
