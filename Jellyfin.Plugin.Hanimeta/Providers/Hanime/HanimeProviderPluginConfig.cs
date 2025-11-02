using System;
using Jellyfin.Plugin.Hanimeta.Providers._Registry;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.Providers.Hanime;

/// <summary>
/// Configuration class for Hanime provider.
/// Contains all metadata and factory methods needed for plugin provider registration.
/// This unifies all Hanime-related components into a single configuration point.
/// </summary>
public class HanimeProviderPluginConfig : IProviderPluginConfig
{
    /// <inheritdoc />
    public string ProviderName => "Hanime";

    /// <inheritdoc />
    public string ProviderId => "hanime";

    /// <inheritdoc />
    public Type MapperType => typeof(HanimeMetadataMapper);

    /// <inheritdoc />
    public Type ExternalIdType => typeof(HanimeMovieExternalId);

    /// <inheritdoc />
    public object CreateApiClientFactory(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, string backendUrl, string apiToken)
    {
        // This method is no longer used since we register factories directly in the registrator
        var logger = loggerFactory.CreateLogger<HanimeUnifiedApiClient>();
        return new Func<HanimeUnifiedApiClient>(() => new HanimeUnifiedApiClient(logger, backendUrl, apiToken));
    }

    /// <inheritdoc />
    public IRemoteMetadataProvider<Movie, MovieInfo> CreateMetadataProvider(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<HanimeUnifiedMetadataProvider>>();
        var apiClientFactory = serviceProvider.GetRequiredService<Func<HanimeUnifiedApiClient>>();
        var mapper = serviceProvider.GetRequiredService<HanimeMetadataMapper>();
        
        return new HanimeUnifiedMetadataProvider(logger, apiClientFactory, mapper);
    }

    /// <inheritdoc />
    public IRemoteImageProvider CreateImageProvider(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<HanimeUnifiedImageProvider>>();
        var apiClientFactory = serviceProvider.GetRequiredService<Func<HanimeUnifiedApiClient>>();
        
        return new HanimeUnifiedImageProvider(logger, apiClientFactory);
    }

    /// <inheritdoc />
    public IExternalUrlProvider CreateExternalUrlProvider(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<HanimeUnifiedExternalUrlProvider>>();
        return new HanimeUnifiedExternalUrlProvider(logger);
    }
}
