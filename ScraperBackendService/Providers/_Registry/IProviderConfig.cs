namespace ScraperBackendService.Providers._Registry;

/// <summary>
/// Interface for provider configuration.
/// Each provider should implement this interface to define its registration metadata.
/// </summary>
public interface IProviderConfig
{
    /// <summary>
    /// Gets the provider name for logging and identification.
    /// Example: "Hanime", "DLsite", "AniList"
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the route prefix for API endpoints.
    /// Example: "hanime" generates /api/hanime/search and /api/hanime/{id}
    /// </summary>
    string RoutePrefix { get; }

    /// <summary>
    /// Gets the cache key prefix for this provider.
    /// Example: "hanime", "dlsite"
    /// </summary>
    string CacheKey { get; }

    /// <summary>
    /// Gets the provider implementation type.
    /// Example: typeof(HanimeProvider)
    /// </summary>
    Type ProviderType { get; }

    /// <summary>
    /// Gets the network client type used by this provider.
    /// Example: typeof(HttpNetworkClient) or typeof(PlaywrightNetworkClient)
    /// </summary>
    Type NetworkClientType { get; }

    /// <summary>
    /// Creates an instance of the provider using dependency injection.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies</param>
    /// <param name="logger">Logger instance for the provider</param>
    /// <returns>Provider instance</returns>
    object CreateProvider(IServiceProvider serviceProvider, Microsoft.Extensions.Logging.ILogger logger);
}
