using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Hanimeta.Client;
using Jellyfin.Plugin.Hanimeta.Extensions;
using Jellyfin.Plugin.Hanimeta.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.Providers.Common;

/// <summary>
/// Base image provider service that eliminates code duplication between providers.
/// </summary>
/// <typeparam name="TMetadata">The metadata type.</typeparam>
/// <typeparam name="TApiClient">The API client type.</typeparam>
public abstract class BaseImageProviderService<TMetadata, TApiClient>
    : IRemoteImageProvider, IHasOrder
    where TMetadata : BaseMetadata
    where TApiClient : BaseScraperApiClient<TMetadata, BaseSearchResult>
{
    private readonly ILogger logger;
    private readonly Func<TApiClient> apiClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseImageProviderService{TMetadata, TApiClient}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="apiClientFactory">The API client factory.</param>
    protected BaseImageProviderService(ILogger logger, Func<TApiClient> apiClientFactory)
    {
        this.logger = logger;
        this.apiClientFactory = apiClientFactory;
    }

    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the provider ID key.
    /// </summary>
    protected abstract string ProviderIdKey { get; }

    /// <summary>
    /// Tries to parse a provider ID from the input string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="id">The parsed ID if successful.</param>
    /// <returns>True if parsing was successful.</returns>
    protected abstract bool TryParseProviderId(string? input, out string id);

    /// <inheritdoc />
    public virtual int Order => 0;

    /// <inheritdoc />
    public bool Supports(BaseItem item) => item is Movie;

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return new[] { ImageType.Primary, ImageType.Backdrop, ImageType.Thumb };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        try
        {
            var providerId = item.GetProviderId(ProviderIdKey);
            if (string.IsNullOrWhiteSpace(providerId))
            {
                if (!string.IsNullOrWhiteSpace(item.Name) &&
                    TryParseProviderId(item.Name, out var parsedId))
                {
                    providerId = parsedId;
                    logger.LogInformationIfEnabled($"[{Name}] Parsed ID from item name '{item.Name}': {providerId}");
                }
                else
                {
                    logger.LogDebugIfEnabled($"[{Name}] No ID found for item: {item.Name}");
                    return Array.Empty<RemoteImageInfo>();
                }
            }

            logger.LogInformationIfEnabled($"[{Name}] Fetching images for ID: {providerId}");

            if (!Plugin.IsConfigurationValid())
            {
                logger.LogErrorIfEnabled($"[{Name}] Invalid plugin configuration");
                return Array.Empty<RemoteImageInfo>();
            }

            using var apiClient = apiClientFactory();
            var metadata = await apiClient.GetMetadataAsync(providerId, cancellationToken).ConfigureAwait(false);

            if (metadata == null)
            {
                logger.LogWarningIfEnabled($"[{Name}] No metadata found for ID: {providerId}");
                return Array.Empty<RemoteImageInfo>();
            }

            var images = new List<RemoteImageInfo>();

            if (!string.IsNullOrWhiteSpace(metadata.Primary))
            {
                images.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Url = metadata.Primary,
                    Type = ImageType.Primary
                });

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

            logger.LogInformationIfEnabled($"[{Name}] Found {images.Count} images for ID: {providerId}");
            return images;
        }
        catch (Exception ex)
        {
            logger.LogErrorIfEnabled($"[{Name}] Error fetching images for item: {item.Name}", ex);
            return Array.Empty<RemoteImageInfo>();
        }
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        using var apiClient = apiClientFactory();
        return await apiClient.GetImageResponseAsync(url, cancellationToken).ConfigureAwait(false);
    }
}
