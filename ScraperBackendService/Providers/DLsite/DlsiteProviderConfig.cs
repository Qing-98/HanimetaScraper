using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScraperBackendService.Core.Net;
using ScraperBackendService.Providers._Registry;

namespace ScraperBackendService.Providers.DLsite;

/// <summary>
/// Configuration class for DLsite provider.
/// Contains all metadata and factory methods needed for provider registration.
/// </summary>
public class DlsiteProviderConfig : IProviderConfig
{
    /// <inheritdoc />
    public string ProviderName => "DLsite";

    /// <inheritdoc />
    public string RoutePrefix => "dlsite";

    /// <inheritdoc />
    public string CacheKey => "dlsite";

    /// <inheritdoc />
    public Type ProviderType => typeof(DlsiteProvider);

    /// <inheritdoc />
    public Type NetworkClientType => typeof(HttpNetworkClient);

    /// <inheritdoc />
    public object CreateProvider(IServiceProvider serviceProvider, ILogger logger)
    {
        var client = serviceProvider.GetRequiredService<HttpNetworkClient>();
        return new DlsiteProvider(client, (ILogger<DlsiteProvider>)logger);
    }
}
