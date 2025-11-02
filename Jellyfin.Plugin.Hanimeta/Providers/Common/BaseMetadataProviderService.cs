using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Hanimeta.Client;
using Jellyfin.Plugin.Hanimeta.Extensions;
using Jellyfin.Plugin.Hanimeta.Helpers;
using Jellyfin.Plugin.Hanimeta.Models;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.Providers.Common;

/// <summary>
/// Base metadata provider service that eliminates code duplication between providers.
/// </summary>
/// <typeparam name="TMetadata">The metadata type.</typeparam>
/// <typeparam name="TPerson">The person type.</typeparam>
/// <typeparam name="TApiClient">The API client type.</typeparam>
/// <typeparam name="TMapper">The metadata mapper type.</typeparam>
public abstract class BaseMetadataProviderService<TMetadata, TPerson, TApiClient, TMapper>
    : IRemoteMetadataProvider<Movie, MovieInfo>, IRemoteSearchProvider<MovieInfo>, IHasOrder
    where TMetadata : BaseMetadata
    where TPerson : BasePerson
    where TApiClient : BaseScraperApiClient<TMetadata, BaseSearchResult>
    where TMapper : BaseMetadataMapper<TMetadata, TPerson, BaseSearchResult>
{
    private readonly ILogger logger;
    private readonly Func<TApiClient> apiClientFactory;
    private readonly TMapper mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseMetadataProviderService{TMetadata, TPerson, TApiClient, TMapper}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="apiClientFactory">The API client factory.</param>
    /// <param name="mapper">The metadata mapper.</param>
    protected BaseMetadataProviderService(
        ILogger logger,
        Func<TApiClient> apiClientFactory,
        TMapper mapper)
    {
        this.logger = logger;
        this.apiClientFactory = apiClientFactory;
        this.mapper = mapper;
    }

    /// <summary>
    /// Gets the provider name (e.g., "Hanime", "DLsite").
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the provider ID key (used in ProviderIds dictionary).
    /// </summary>
    protected abstract string ProviderIdKey { get; }

    /// <summary>
    /// Tries to parse a provider ID from the input string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="id">The parsed ID if successful.</param>
    /// <returns>True if parsing was successful.</returns>
    protected abstract bool TryParseProviderId(string? input, out string id);

    /// <summary>
    /// Gets the people array from metadata.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <returns>The people collection.</returns>
    protected abstract IEnumerable<TPerson> GetPeople(TMetadata metadata);

    /// <inheritdoc />
    public virtual int Order => 0;

    /// <inheritdoc />
    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformationIfEnabled(
                $"[{Name}] GetMetadata: Name={info?.Name}, ProviderIds={string.Join(", ", info?.ProviderIds?.Select(p => $"{p.Key}:{p.Value}") ?? Array.Empty<string>())}");

            var result = new MetadataResult<Movie>
            {
                Item = new Movie(),
                HasMetadata = false
            };

            if (info == null)
            {
                logger.LogErrorIfEnabled($"[{Name}] MovieInfo is null");
                return result;
            }

            var cleanedName = FilenameCleaner.Clean(info.Name);
            var cleanedOriginal = FilenameCleaner.Clean(info.OriginalTitle);

            // Try to get ID
            string? id = null;
            if (info.ProviderIds != null &&
                info.ProviderIds.TryGetValue(ProviderIdKey, out id) &&
                !string.IsNullOrWhiteSpace(id))
            {
                // ID found
            }

            // Parse ID from filename
            if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(cleanedName))
            {
                if (TryParseProviderId(cleanedName, out var parsedId))
                {
                    id = parsedId;
                    logger.LogInformationIfEnabled($"[{Name}] Parsed ID from name '{cleanedName}': {id}");
                }
            }

            // Perform title search
            if (string.IsNullOrWhiteSpace(id))
            {
                var query = !string.IsNullOrWhiteSpace(cleanedOriginal) ? cleanedOriginal :
                            !string.IsNullOrWhiteSpace(cleanedName) ? cleanedName :
                            (info.OriginalTitle ?? info.Name);

                if (!string.IsNullOrWhiteSpace(query))
                {
                    logger.LogInformationIfEnabled($"[{Name}] No ID found — performing title search: {query}");
                    var searchResults = await PerformTitleSearchAsync(query, cancellationToken).ConfigureAwait(false);
                    var first = searchResults?.FirstOrDefault();
                    if (first != null && first.ProviderIds != null &&
                        first.ProviderIds.TryGetValue(ProviderIdKey, out var foundId) &&
                        !string.IsNullOrWhiteSpace(foundId))
                    {
                        id = foundId;
                        logger.LogInformationIfEnabled($"[{Name}] Title search matched ID: {id}");
                    }
                }

                if (string.IsNullOrWhiteSpace(id))
                {
                    logger.LogDebugIfEnabled($"[{Name}] No ID found for {info.Name}");
                    return result;
                }
            }

            if (!Plugin.IsConfigurationValid())
            {
                logger.LogErrorIfEnabled($"[{Name}] Invalid plugin configuration");
                return result;
            }

            logger.LogInformationIfEnabled($"[{Name}] Fetching metadata for ID: {id}");

            using var apiClient = apiClientFactory();
            var metadata = await apiClient.GetMetadataAsync(id, cancellationToken).ConfigureAwait(false);

            if (metadata == null)
            {
                logger.LogWarningIfEnabled($"[{Name}] No metadata found for ID: {id}");
                return result;
            }

            mapper.MapToMovie(metadata, result.Item, info.Name);

            foreach (var person in mapper.CreatePersonInfos(GetPeople(metadata)))
            {
                try
                {
                    result.AddPerson(person);
                }
                catch (Exception ex)
                {
                    logger.LogDebugIfEnabled($"[{Name}] Failed to add person {person?.Name}: {ex.Message}");
                }
            }

            result.HasMetadata = true;
            logger.LogInformationIfEnabled($"[{Name}] Successfully fetched metadata for ID: {id}, Title: {result.Item.Name}");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogErrorIfEnabled($"[{Name}] Unexpected error in GetMetadata", ex);
            return new MetadataResult<Movie> { Item = new Movie(), HasMetadata = false };
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformationIfEnabled(
                $"[{Name}] GetSearchResults: Name='{searchInfo.Name}', Year={searchInfo.Year}, ProviderIds={string.Join(", ", searchInfo.ProviderIds.Select(p => $"{p.Key}:{p.Value}"))}");

            // If ID exists, return details directly
            if (searchInfo.ProviderIds != null &&
                searchInfo.ProviderIds.TryGetValue(ProviderIdKey, out var existingId) &&
                !string.IsNullOrWhiteSpace(existingId))
            {
                logger.LogInformationIfEnabled($"[{Name}] Found ID in ProviderIds: {existingId}");
                var detailResult = await GetDetailByIdAsync(existingId, cancellationToken).ConfigureAwait(false);
                if (detailResult != null)
                {
                    return new[] { detailResult };
                }
            }

            // Perform title search
            var query = searchInfo.OriginalTitle ?? searchInfo.Name;
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<RemoteSearchResult>();
            }

            var cleanedQuery = FilenameCleaner.Clean(query);
            if (string.IsNullOrWhiteSpace(cleanedQuery))
            {
                return Array.Empty<RemoteSearchResult>();
            }

            logger.LogInformationIfEnabled($"[{Name}] Performing title search: {cleanedQuery}");
            return await PerformTitleSearchAsync(cleanedQuery, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogErrorIfEnabled($"[{Name}] Unexpected error in GetSearchResults", ex);
            return Array.Empty<RemoteSearchResult>();
        }
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        using var apiClient = apiClientFactory();
        return await apiClient.GetImageResponseAsync(url, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RemoteSearchResult?> GetDetailByIdAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            using var apiClient = apiClientFactory();
            var metadata = await apiClient.GetMetadataAsync(id, cancellationToken).ConfigureAwait(false);

            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Title))
            {
                logger.LogInformationIfEnabled($"[{Name}] Found content by ID {id}: {metadata.Title}");
                return new RemoteSearchResult
                {
                    Name = metadata.Title,
                    Overview = metadata.Description,
                    ProductionYear = metadata.Year,
                    ImageUrl = metadata.Primary,
                    ProviderIds = { [ProviderIdKey] = id }
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogErrorIfEnabled($"[{Name}] Failed to get detail for ID: {id}", ex);
        }

        return null;
    }

    private async Task<IEnumerable<RemoteSearchResult>> PerformTitleSearchAsync(string title, CancellationToken cancellationToken)
    {
        try
        {
            using var apiClient = apiClientFactory();
            var searchResults = await apiClient.SearchAsync(title, 10, cancellationToken).ConfigureAwait(false);

            var results = searchResults.Select(mapper.MapToSearchResult).ToList();
            logger.LogInformationIfEnabled($"[{Name}] Title search returned {results.Count} results for: {title}");

            return results;
        }
        catch (Exception ex)
        {
            logger.LogErrorIfEnabled($"[{Name}] Failed to perform title search for: {title}", ex);
            return Array.Empty<RemoteSearchResult>();
        }
    }
}
