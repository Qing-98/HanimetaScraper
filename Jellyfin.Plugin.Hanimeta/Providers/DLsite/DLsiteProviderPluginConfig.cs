using System;
using Jellyfin.Plugin.Hanimeta.Providers._Registry;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.Providers.DLsite;

/// <summary>
/// Configuration class for DLsite provider.
/// Contains all metadata and factory methods needed for plugin provider registration.
/// This unifies all DLsite-related components into a single configuration point.
/// </summary>
public class DLsiteProviderPluginConfig : IProviderPluginConfig
{
    /// <inheritdoc />
    public string ProviderName => "DLsite";

    /// <inheritdoc />
    public string ProviderId => "dlsite";

    /// <inheritdoc />
    public Type MapperType => typeof(DLsiteMetadataMapper);

    /// <inheritdoc />
    public Type ExternalIdType => typeof(DLsiteMovieExternalId);

    /// <inheritdoc />
    public object CreateApiClientFactory(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, string backendUrl, string apiToken)
    {
        // This method is no longer used since we register factories directly in the registrator
        var logger = loggerFactory.CreateLogger<DLsiteUnifiedApiClient>();
        return new Func<DLsiteUnifiedApiClient>(() => new DLsiteUnifiedApiClient(logger, backendUrl, apiToken));
    }

    /// <inheritdoc />
    public IRemoteMetadataProvider<Movie, MovieInfo> CreateMetadataProvider(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<DLsiteUnifiedMetadataProvider>>();
        var apiClientFactory = serviceProvider.GetRequiredService<Func<DLsiteUnifiedApiClient>>();
        var mapper = serviceProvider.GetRequiredService<DLsiteMetadataMapper>();
        
        return new DLsiteUnifiedMetadataProvider(logger, apiClientFactory, mapper);
    }

    /// <inheritdoc />
    public IRemoteImageProvider CreateImageProvider(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<DLsiteUnifiedImageProvider>>();
        var apiClientFactory = serviceProvider.GetRequiredService<Func<DLsiteUnifiedApiClient>>();
        
        return new DLsiteUnifiedImageProvider(logger, apiClientFactory);
    }

    /// <inheritdoc />
    public IExternalUrlProvider CreateExternalUrlProvider(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<DLsiteUnifiedExternalUrlProvider>>();
        return new DLsiteUnifiedExternalUrlProvider(logger);
    }
}
