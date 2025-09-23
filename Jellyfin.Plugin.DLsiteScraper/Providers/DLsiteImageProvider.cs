using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DLsiteScraper.Providers;

/// <summary>
/// DLsite image provider.
/// </summary>
public class DLsiteImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly ILogger<DLsiteImageProvider> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DLsiteImageProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DLsiteImageProvider(ILogger<DLsiteImageProvider> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public string Name => "DLsite";

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public bool Supports(BaseItem item) => item is Movie;

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return new[] { ImageType.Primary, ImageType.Backdrop };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var id = item.GetProviderId("DLsite");
        if (string.IsNullOrWhiteSpace(id))
        {
            logger.LogDebug("No DLsite ID found for item {Name}", item.Name);
            return new List<RemoteImageInfo>();
        }

        logger.LogInformation("Fetching images for DLsite ID: {Id}", id);

        var backendUrl = Plugin.PluginConfig?.BackendUrl?.TrimEnd('/') ?? "http://localhost:8585";
        var requestUrl = $"{backendUrl}/api/dlsite/{id}";

        using var client = CreateClientWithToken();
        try
        {
            var response = await client.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonDocument.Parse(json).RootElement;

            // Check if API response is successful and extract data
            if (!apiResponse.TryGetProperty("success", out var successProp) ||
                !successProp.GetBoolean() ||
                !apiResponse.TryGetProperty("data", out var root))
            {
                logger.LogWarning("API response indicates failure or missing data for id={Id}", id);
                return new List<RemoteImageInfo>();
            }

            var images = new List<RemoteImageInfo>();

            // Primary image (使用小写属性名)
            if (root.TryGetProperty("primary", out var primary) && !string.IsNullOrWhiteSpace(primary.GetString()))
            {
                var primaryUrl = primary.GetString()!;
                logger.LogDebug("Found primary image: {Url}", primaryUrl);
                images.Add(new RemoteImageInfo
                {
                    Type = ImageType.Primary,
                    Url = primaryUrl,
                    ProviderName = Name
                });
            }

            // Backdrop image (使用小写属性名)
            if (root.TryGetProperty("backdrop", out var backdrop) && !string.IsNullOrWhiteSpace(backdrop.GetString()))
            {
                var backdropUrl = backdrop.GetString()!;
                logger.LogDebug("Found backdrop image: {Url}", backdropUrl);
                images.Add(new RemoteImageInfo
                {
                    Type = ImageType.Backdrop,
                    Url = backdropUrl,
                    ProviderName = Name
                });
            }

            // Thumbnails as backdrops (使用小写属性名)
            if (root.TryGetProperty("thumbnails", out var thumbnails) && thumbnails.ValueKind == JsonValueKind.Array)
            {
                foreach (var thumb in thumbnails.EnumerateArray())
                {
                    var url = thumb.GetString();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        logger.LogDebug("Found thumbnail image: {Url}", url);
                        images.Add(new RemoteImageInfo
                        {
                            Type = ImageType.Backdrop,
                            Url = url,
                            ProviderName = Name
                        });
                    }
                }
            }

            logger.LogInformation("Found {Count} images for DLsite ID: {Id}", images.Count, id);
            return images;
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex, "Failed to fetch images for DLsite ID: {Id}", id);
            return new List<RemoteImageInfo>();
        }
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        logger.LogDebug("Fetching image from URL: {Url}", url);
        return new HttpClient().GetAsync(url, cancellationToken);
    }

    private HttpClient CreateClientWithToken()
    {
        var client = new HttpClient();
        var token = Plugin.PluginConfig?.ApiToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Add("X-API-Token", token);
        }

        return client;
    }
}
