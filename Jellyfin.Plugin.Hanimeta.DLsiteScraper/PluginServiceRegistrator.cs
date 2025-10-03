using System;
using Jellyfin.Plugin.Hanimeta.Common.Extensions;
using Jellyfin.Plugin.Hanimeta.Common.Registration;
using Jellyfin.Plugin.Hanimeta.DLsiteScraper.Client;
using Jellyfin.Plugin.Hanimeta.DLsiteScraper.ExternalIds;
using Jellyfin.Plugin.Hanimeta.DLsiteScraper.ExternalUrls;
using Jellyfin.Plugin.Hanimeta.DLsiteScraper.Providers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.DLsiteScraper
{
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
            services.AddSingleton<Func<DLsiteApiClient>>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<DLsiteApiClient>>();
                return () => new DLsiteApiClient(logger, Plugin.GetBackendUrl(), Plugin.GetApiToken());
            });

            // Register metadata providers - these need to be registered with the correct interfaces
            services.AddSingleton<IMetadataProvider<Movie>, DLsiteMetadataProvider>();
            services.AddSingleton<IRemoteImageProvider, DLsiteImageProvider>();

            // Register external ID provider - this needs to be registered with the correct interface
            services.AddSingleton<IExternalId, MovieId>();

            // Register external URL provider so Jellyfin UI can retrieve detail page URLs
            services.AddSingleton<IExternalUrlProvider, DLsiteExternalUrlProvider>();
        }
    }
}
