using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScraperBackendService.Core.Net;
using ScraperBackendService.Providers._Registry;

namespace ScraperBackendService.Providers.Hanime;

/// <summary>
/// Configuration class for Hanime provider.
/// Contains all metadata and factory methods needed for provider registration.
/// </summary>
public class HanimeProviderConfig : IProviderConfig
{
    /// <inheritdoc />
    public string ProviderName => "Hanime";

    /// <inheritdoc />
    public string RoutePrefix => "hanime";

    /// <inheritdoc />
    public string CacheKey => "hanime";

    /// <inheritdoc />
    public Type ProviderType => typeof(HanimeProvider);

    /// <inheritdoc />
    public Type NetworkClientType => typeof(PlaywrightNetworkClient);

    /// <inheritdoc />
    public object CreateProvider(IServiceProvider serviceProvider, ILogger logger)
    {
        var client = serviceProvider.GetRequiredService<PlaywrightNetworkClient>();
        return new HanimeProvider(client, (ILogger<HanimeProvider>)logger);
    }
}
