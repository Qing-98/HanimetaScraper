using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Hanimeta.Common.Extensions;
using Jellyfin.Plugin.Hanimeta.Common.Helpers;
using Jellyfin.Plugin.Hanimeta.DLsiteScraper.Client;
using Jellyfin.Plugin.Hanimeta.DLsiteScraper.Helpers;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.DLsiteScraper.Providers;

/// <summary>
/// DLsite metadata provider that interacts with backend service.
/// </summary>
public class DLsiteMetadataProvider
    : IRemoteMetadataProvider<Movie, MovieInfo>, IRemoteSearchProvider<MovieInfo>, IHasOrder
{
    private readonly ILogger<DLsiteMetadataProvider> logger;
    private readonly Func<DLsiteApiClient> apiClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DLsiteMetadataProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="apiClientFactory">Factory to create API client instances.</param>
    public DLsiteMetadataProvider(ILogger<DLsiteMetadataProvider> logger, Func<DLsiteApiClient> apiClientFactory)
    {
        this.logger = logger;
        this.apiClientFactory = apiClientFactory;
    }

    /// <inheritdoc />
    public string Name => "DLsite";

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public async Task<MetadataResult<Movie>> GetMetadata(
        MovieInfo info,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformationIfEnabled(
                $"GetMetadata called with MovieInfo: Name={info?.Name}, ProviderIds={string.Join(", ", info?.ProviderIds?.Select(p => $"{p.Key}:{p.Value}") ?? Array.Empty<string>())}");

            var result = new MetadataResult<Movie>
            {
                Item = new Movie(),
                HasMetadata = false
            };

            if (info == null)
            {
                logger.LogErrorIfEnabled("MovieInfo is null");
                return result;
            }

            // Pre-clean filenames for better parsing
            var cleanedName = FilenameCleaner.Clean(info.Name);
            var cleanedOriginal = FilenameCleaner.Clean(info.OriginalTitle);

            // Try to get DLsite ID from provider IDs
            string? id = null;
            if (info.ProviderIds != null &&
                info.ProviderIds.TryGetValue("DLsite", out id) &&
                !string.IsNullOrWhiteSpace(id))
            {
                // ID found in provider IDs
            }

            // If no ID found, try to parse from the cleaned name
            if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(cleanedName))
            {
                if (TryParseDLsiteId(cleanedName, out var parsedId))
                {
                    id = parsedId;
                    logger.LogInformationIfEnabled($"Parsed DLsite ID from name '{cleanedName}': {id}");
                }
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                // No explicit ID — try a title search automatically (use first match if available)
                var query = !string.IsNullOrWhiteSpace(cleanedOriginal) ? cleanedOriginal :
                            !string.IsNullOrWhiteSpace(cleanedName) ? cleanedName :
                            (info.OriginalTitle ?? info.Name);

                if (!string.IsNullOrWhiteSpace(query))
                {
                    logger.LogInformationIfEnabled($"No DLsite ID found — performing title search: {query}");
                    var searchResults = await PerformTitleSearchAsync(query, cancellationToken).ConfigureAwait(false);
                    var first = searchResults?.FirstOrDefault();
                    if (first != null && first.ProviderIds != null && first.ProviderIds.TryGetValue("DLsite", out var foundId) && !string.IsNullOrWhiteSpace(foundId))
                    {
                        id = foundId;
                        logger.LogInformationIfEnabled($"Title search matched DLsite ID: {id}");
                    }
                }

                if (string.IsNullOrWhiteSpace(id))
                {
                    logger.LogDebugIfEnabled($"No DLsite ID found for {info.Name}");
                    return result;
                }
            }

            // Validate configuration
            if (!Plugin.IsConfigurationValid())
            {
                logger.LogErrorIfEnabled("Invalid plugin configuration");
                return result;
            }

            logger.LogInformationIfEnabled($"Fetching metadata for DLsite ID: {id}");

            using var apiClient = this.apiClientFactory();
            var metadata = await apiClient.GetMetadataAsync(id, cancellationToken).ConfigureAwait(false);

            if (metadata == null)
            {
                logger.LogWarningIfEnabled($"No metadata found for DLsite ID: {id}");
                return result;
            }

            // Map metadata to Jellyfin entities
            MetadataMapper.MapToMovie(metadata, result.Item, info.Name);

            // Add people into MetadataResult so Jellyfin core handles them
            foreach (var person in MetadataMapper.CreatePersonInfos(metadata))
            {
                try
                {
                    result.AddPerson(person);
                }
                catch (Exception ex)
                {
                    logger.LogDebugIfEnabled($"Failed to add person {person?.Name}: {ex.Message}");
                }
            }

            result.HasMetadata = true;

            logger.LogInformationIfEnabled($"Successfully fetched metadata for DLsite ID: {id}, Title: {result.Item.Name}");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogErrorIfEnabled("Unexpected error in GetMetadata", ex);
            return new MetadataResult<Movie> { Item = new Movie(), HasMetadata = false };
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        MovieInfo searchInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformationIfEnabled(
                $"GetSearchResults called with: Name='{searchInfo.Name}', Year={searchInfo.Year}, ProviderIds={string.Join(", ", searchInfo.ProviderIds.Select(p => $"{p.Key}:{p.Value}"))}");

            var results = new List<RemoteSearchResult>();

            // First priority: Check if there's a DLsite ID in ProviderIds
            string? existingId = null;
            if (searchInfo.ProviderIds != null &&
                searchInfo.ProviderIds.TryGetValue("DLsite", out existingId) &&
                !string.IsNullOrWhiteSpace(existingId))
            {
                logger.LogInformationIfEnabled($"Found DLsite ID in ProviderIds: {existingId}");
                var detailResult = await GetDetailByIdAsync(existingId, cancellationToken).ConfigureAwait(false);
                if (detailResult != null)
                {
                    results.Add(detailResult);
                }

                return results;
            }

            // Build search query: prefer OriginalTitle, then Name
            var query = searchInfo.OriginalTitle ?? searchInfo.Name;
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            // Clean the query to remove noise before searching
            var cleanedQuery = FilenameCleaner.Clean(query);
            if (string.IsNullOrWhiteSpace(cleanedQuery))
            {
                return results;
            }

            // Perform title search using cleaned query
            logger.LogInformationIfEnabled($"Performing title search: {cleanedQuery}");
            return await PerformTitleSearchAsync(cleanedQuery, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogErrorIfEnabled("Unexpected error in GetSearchResults", ex);
            return Array.Empty<RemoteSearchResult>();
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

    private async Task<RemoteSearchResult?> GetDetailByIdAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            using var apiClient = this.apiClientFactory();
            var metadata = await apiClient.GetMetadataAsync(id, cancellationToken).ConfigureAwait(false);

            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Title))
            {
                logger.LogInformationIfEnabled($"Found content by ID {id}: {metadata.Title}");
                return new RemoteSearchResult
                {
                    Name = metadata.Title,
                    Overview = metadata.Description,
                    ProductionYear = metadata.Year,
                    ImageUrl = metadata.Primary,
                    ProviderIds = { ["DLsite"] = id }
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogErrorIfEnabled($"Failed to get detail for DLsite ID: {id}", ex);
        }

        return null;
    }

    private async Task<IEnumerable<RemoteSearchResult>> PerformTitleSearchAsync(string title, CancellationToken cancellationToken)
    {
        try
        {
            using var apiClient = this.apiClientFactory();
            var searchResults = await apiClient.SearchAsync(title, 10, cancellationToken).ConfigureAwait(false);

            var results = searchResults.Select(MetadataMapper.MapToSearchResult).ToList();
            logger.LogInformationIfEnabled($"Title search returned {results.Count} results for: {title}");

            return results;
        }
        catch (Exception ex)
        {
            logger.LogErrorIfEnabled($"Failed to perform title search for: {title}", ex);
            return Array.Empty<RemoteSearchResult>();
        }
    }
}
