using System;
using Jellyfin.Plugin.Hanimeta.Extensions;
using Jellyfin.Plugin.Hanimeta.Providers._Registry;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta;

/// <summary>
/// Plugin service registrator for the unified Hanimeta plugin.
/// Registers all providers and services for dependency injection using the unified provider registry system.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        try
        {
            // Set up logging delegate
            LoggingExtensions.IsLoggingEnabled = () => Plugin.IsLoggingEnabled();

            // Register all providers from the unified registry
            var configs = ProviderPluginRegistry.AllConfigurations;

            foreach (var config in configs)
            {
                try
                {
                    RegisterProvider(serviceCollection, config);
                }
                catch (Exception ex)
                {
                    // Use simple console logging for critical errors during registration
                    System.Diagnostics.Debug.WriteLine($"[Hanimeta] Failed to register provider {config.ProviderName}: {ex}");
                    throw; // Re-throw to prevent partial registration
                }
            }
        }
        catch (Exception ex)
        {
            // Fallback logging since we might not have a logger yet
            System.Diagnostics.Debug.WriteLine($"[Hanimeta] Fatal error during service registration: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Registers a complete provider suite (metadata, image, external ID, external URL) using the provider configuration.
    /// This generic method handles all the boilerplate for any provider.
    /// </summary>
    /// <remarks>
    /// To add a new provider in the future:
    /// 1. Create a new ProviderPluginConfig class implementing IProviderPluginConfig
    /// 2. Add it to ProviderPluginRegistry.AllConfigurations
    /// 3. The provider will be automatically registered
    /// 
    /// This replaces the need for manual ProviderRegistrationBuilder calls.
    /// </remarks>
    /// <param name="serviceCollection">The service collection to register with.</param>
    /// <param name="config">The provider configuration.</param>
    private static void RegisterProvider(
        IServiceCollection serviceCollection,
        IProviderPluginConfig config)
    {
        // Register mapper singleton
        serviceCollection.AddSingleton(config.MapperType);

        // Register the API client factory with the correct type
        // We need to register it specifically for each provider type
        RegisterApiClientFactory(serviceCollection, config);

        // Register metadata provider singleton
        serviceCollection.AddSingleton<IRemoteMetadataProvider<Movie, MovieInfo>>(
            provider => config.CreateMetadataProvider(provider));

        // Register image provider singleton
        serviceCollection.AddSingleton<IRemoteImageProvider>(
            provider => config.CreateImageProvider(provider));

        // Register external ID singleton
        serviceCollection.AddSingleton(typeof(IExternalId), config.ExternalIdType);

        // Register external URL provider singleton
        serviceCollection.AddSingleton<IExternalUrlProvider>(
            provider => config.CreateExternalUrlProvider(provider));
    }

    /// <summary>
    /// Registers the API client factory for the specific provider type.
    /// </summary>
    /// <param name="serviceCollection">The service collection.</param>
    /// <param name="config">The provider configuration.</param>
    private static void RegisterApiClientFactory(IServiceCollection serviceCollection, IProviderPluginConfig config)
    {
        // Based on the provider name, register the correct factory type
        switch (config.ProviderName.ToLowerInvariant())
        {
            case "hanime":
                serviceCollection.AddSingleton<Func<Jellyfin.Plugin.Hanimeta.Providers.Hanime.HanimeUnifiedApiClient>>(serviceProvider =>
                {
                    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<Jellyfin.Plugin.Hanimeta.Providers.Hanime.HanimeUnifiedApiClient>();
                    return () => new Jellyfin.Plugin.Hanimeta.Providers.Hanime.HanimeUnifiedApiClient(
                        logger, 
                        Plugin.GetBackendUrl(), 
                        Plugin.GetApiToken());
                });
                break;

            case "dlsite":
                serviceCollection.AddSingleton<Func<Jellyfin.Plugin.Hanimeta.Providers.DLsite.DLsiteUnifiedApiClient>>(serviceProvider =>
                {
                    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<Jellyfin.Plugin.Hanimeta.Providers.DLsite.DLsiteUnifiedApiClient>();
                    return () => new Jellyfin.Plugin.Hanimeta.Providers.DLsite.DLsiteUnifiedApiClient(
                        logger, 
                        Plugin.GetBackendUrl(), 
                        Plugin.GetApiToken());
                });
                break;

            default:
                // For unknown providers, we can't register the factory
                System.Diagnostics.Debug.WriteLine($"[Hanimeta] Unknown provider type: {config.ProviderName}");
                break;
        }
    }
}
