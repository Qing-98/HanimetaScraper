using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Hanimeta.Common.Extensions;
using Jellyfin.Plugin.Hanimeta.DLsiteScraper.Client;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.DLsiteScraper.Providers;

/// <summary>
/// DLsite image provider that fetches images from the backend service.
/// </summary>
public class DLsiteImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly ILogger<DLsiteImageProvider> logger;
    private readonly Func<DLsiteApiClient> apiClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DLsiteImageProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="apiClientFactory">Factory to create API client instances.</param>
    public DLsiteImageProvider(ILogger<DLsiteImageProvider> logger, Func<DLsiteApiClient> apiClientFactory)
    {
        this.logger = logger;
        this.apiClientFactory = apiClientFactory;
    }

    /// <inheritdoc />
    public string Name => "DLsite";

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public bool Supports(BaseItem item) => item is Movie;

    /// <inheritdoc />
    public System.Collections.Generic.IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return new[] { ImageType.Primary, ImageType.Backdrop, ImageType.Thumb };
    }

    /// <inheritdoc />
    public async Task<System.Collections.Generic.IEnumerable<RemoteImageInfo>> GetImages(
        BaseItem item,
        CancellationToken cancellationToken)
    {
        try
        {
            var dlsiteId = item.GetProviderId("DLsite");
            if (string.IsNullOrWhiteSpace(dlsiteId))
            {
                // Try to parse from item name
                if (!string.IsNullOrWhiteSpace(item.Name) &&
                    TryParseDLsiteId(item.Name, out var parsedId))
                {
                    dlsiteId = parsedId;
                    logger.LogInformationIfEnabled($"Parsed DLsite ID from item name '{item.Name}': {dlsiteId}");
                }
                else
                {
                    logger.LogDebugIfEnabled($"No DLsite ID found for item: {item.Name}");
                    return Array.Empty<RemoteImageInfo>();
                }
            }

            logger.LogInformationIfEnabled($"Fetching images for DLsite ID: {dlsiteId}");

            if (!Plugin.IsConfigurationValid())
            {
                logger.LogErrorIfEnabled("Invalid plugin configuration");
                return Array.Empty<RemoteImageInfo>();
            }

            using var apiClient = this.apiClientFactory();
            var metadata = await apiClient.GetMetadataAsync(dlsiteId, cancellationToken).ConfigureAwait(false);

            if (metadata == null)
            {
                logger.LogWarningIfEnabled($"No metadata found for DLsite ID: {dlsiteId}");
                return Array.Empty<RemoteImageInfo>();
            }

            var images = new List<RemoteImageInfo>();

            // Add primary image if available
            if (!string.IsNullOrWhiteSpace(metadata.Primary))
            {
                images.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Url = metadata.Primary,
                    Type = ImageType.Primary
                });

                // Also add as backdrop and thumb
                images.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Url = metadata.Primary,
                    Type = ImageType.Backdrop
                });

                images.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Url = metadata.Primary,
                    Type = ImageType.Thumb
                });
            }

            logger.LogInformationIfEnabled($"Found {images.Count} images for DLsite ID: {dlsiteId}");
            return images;
        }
        catch (Exception ex)
        {
            logger.LogErrorIfEnabled($"Error fetching images for item: {item.Name}", ex);
            return Array.Empty<RemoteImageInfo>();
        }
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        using var apiClient = this.apiClientFactory();
        return await apiClient.GetImageResponseAsync(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to parse a DLsite ID from various input formats.
    /// </summary>
    /// <param name="input">The input string to parse.</param>
    /// <param name="id">The extracted DLsite ID if parsing succeeds.</param>
    /// <returns>True if a valid ID was extracted, false otherwise.</returns>
    private static bool TryParseDLsiteId(string? input, out string id)
    {
        id = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = input.Trim();

        // Create regex patterns locally to avoid static field naming conflicts
        var dlsiteIdRegex = new Regex(@"^(RJ|VJ)\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var urlIdRegex = new Regex(@"product_id/((?:RJ|VJ)\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Check if it's a DLsite ID format (RJ123456 or VJ123456)
        if (dlsiteIdRegex.IsMatch(cleaned))
        {
            id = cleaned.ToUpperInvariant();
            return true;
        }

        // Check if it's a URL containing the ID
        var urlMatch = urlIdRegex.Match(cleaned);
        if (urlMatch.Success)
        {
            id = urlMatch.Groups[1].Value.ToUpperInvariant();
            return true;
        }

        return false;
    }
}
