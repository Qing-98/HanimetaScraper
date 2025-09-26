using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HanimeScraper.Client;
using Jellyfin.Plugin.HanimeScraper.Configuration;
using Jellyfin.Plugin.HanimeScraper.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HanimeScraper.Providers;

/// <summary>
/// Hanime image provider that fetches images from the backend service.
/// </summary>
public class HanimeImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly ILogger<HanimeImageProvider> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HanimeImageProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public HanimeImageProvider(ILogger<HanimeImageProvider> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Hanime";

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
            var hanimeId = item.GetProviderId("Hanime");
            if (string.IsNullOrWhiteSpace(hanimeId))
            {
                // Try to parse from item name
                if (!string.IsNullOrWhiteSpace(item.Name) &&
                    TryParseHanimeId(item.Name, out var parsedId))
                {
                    hanimeId = parsedId;
                    logger.LogInformationIfEnabled($"Parsed Hanime ID from item name '{item.Name}': {hanimeId}");
                }
                else
                {
                    logger.LogDebugIfEnabled($"No Hanime ID found for item: {item.Name}");
                    return Array.Empty<RemoteImageInfo>();
                }
            }

            logger.LogInformationIfEnabled($"Fetching images for Hanime ID: {hanimeId}");

            if (!ConfigurationManager.IsConfigurationValid())
            {
                logger.LogErrorIfEnabled("Invalid plugin configuration");
                return Array.Empty<RemoteImageInfo>();
            }

            using var apiClient = CreateApiClient();
            var metadata = await apiClient.GetMetadataAsync(hanimeId, cancellationToken).ConfigureAwait(false);

            if (metadata == null)
            {
                logger.LogWarningIfEnabled($"No metadata found for Hanime ID: {hanimeId}");
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

            logger.LogInformationIfEnabled($"Found {images.Count} images for Hanime ID: {hanimeId}");
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
        using var apiClient = CreateApiClient();
        return await apiClient.GetImageResponseAsync(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to parse a Hanime ID from various input formats.
    /// </summary>
    /// <param name="input">The input string to parse.</param>
    /// <param name="id">The extracted Hanime ID if parsing succeeds.</param>
    /// <returns>True if a valid ID was extracted, false otherwise.</returns>
    private static bool TryParseHanimeId(string? input, out string id)
    {
        id = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = input.Trim();

        // Create regex patterns locally to avoid static field naming conflicts
        // Make numeric ID pattern more strict - require 4+ digits to avoid false positives
        var numericIdRegex = new Regex(@"^\d{4,}$", RegexOptions.Compiled);
        var urlIdRegex = new Regex(@"[?&]v=(\d+)", RegexOptions.Compiled);

        // Check if it's a pure numeric ID with at least 4 digits
        if (numericIdRegex.IsMatch(cleaned))
        {
            id = cleaned;
            return true;
        }

        // Check if it's a URL containing the ID
        var urlMatch = urlIdRegex.Match(cleaned);
        if (urlMatch.Success)
        {
            id = urlMatch.Groups[1].Value;
            return true;
        }

        return false;
    }

    private HanimeApiClient CreateApiClient()
    {
        return new HanimeApiClient(
            logger as ILogger<HanimeApiClient> ??
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HanimeApiClient>.Instance,
            ConfigurationManager.GetBackendUrl(),
            ConfigurationManager.GetApiToken(),
            ConfigurationManager.IsLoggingEnabled());
    }
}
