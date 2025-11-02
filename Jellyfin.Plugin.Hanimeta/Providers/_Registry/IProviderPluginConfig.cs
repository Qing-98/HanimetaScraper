using System;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.Providers._Registry;

/// <summary>
/// Interface for plugin provider configuration.
/// Each provider should implement this interface to define its registration metadata for Jellyfin plugin integration.
/// This is the frontend counterpart to the backend's IProviderConfig.
/// </summary>
public interface IProviderPluginConfig
{
    /// <summary>
    /// Gets the provider name for logging and identification.
    /// Should match the backend provider name.
    /// Example: "Hanime", "DLsite", "AniList"
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the provider identifier used for API client configuration.
    /// Should match the backend route prefix.
    /// Example: "hanime", "dlsite"
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Gets the metadata mapper type for this provider.
    /// Example: typeof(HanimeMetadataMapper)
    /// </summary>
    Type MapperType { get; }

    /// <summary>
    /// Gets the external ID type for this provider.
    /// Example: typeof(HanimeMovieExternalId)
    /// </summary>
    Type ExternalIdType { get; }

    /// <summary>
    /// Creates an API client factory for this provider.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies</param>
    /// <param name="loggerFactory">Logger factory for creating typed loggers</param>
    /// <param name="backendUrl">Backend service URL</param>
    /// <param name="apiToken">API authentication token</param>
    /// <returns>API client factory function</returns>
    object CreateApiClientFactory(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, string backendUrl, string apiToken);

    /// <summary>
    /// Creates a metadata provider instance for this provider.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies</param>
    /// <returns>Metadata provider instance</returns>
    IRemoteMetadataProvider<Movie, MovieInfo> CreateMetadataProvider(IServiceProvider serviceProvider);

    /// <summary>
    /// Creates an image provider instance for this provider.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies</param>
    /// <returns>Image provider instance</returns>
    IRemoteImageProvider CreateImageProvider(IServiceProvider serviceProvider);

    /// <summary>
    /// Creates an external URL provider instance for this provider.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies</param>
    /// <returns>External URL provider instance</returns>
    IExternalUrlProvider CreateExternalUrlProvider(IServiceProvider serviceProvider);
}
