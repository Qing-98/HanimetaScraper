using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HanimeScraper.Client;
using Jellyfin.Plugin.HanimeScraper.Configuration;
using Jellyfin.Plugin.HanimeScraper.Extensions;
using Jellyfin.Plugin.HanimeScraper.Helpers;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HanimeScraper.Providers;

/// <summary>
/// Hanime metadata provider that interacts with backend service.
/// </summary>
public class HanimeMetadataProvider
    : IRemoteMetadataProvider<Movie, MovieInfo>, IRemoteSearchProvider<MovieInfo>, IHasOrder
{
    private readonly ILogger<HanimeMetadataProvider> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HanimeMetadataProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public HanimeMetadataProvider(ILogger<HanimeMetadataProvider> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Hanime";

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

            // Try to get Hanime ID from provider IDs
            string? id = null;
            if (info.ProviderIds != null &&
                info.ProviderIds.TryGetValue("Hanime", out id) &&
                !string.IsNullOrWhiteSpace(id))
            {
                // ID found in provider IDs
            }

            // If no ID found, try to parse from the name
            if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(info.Name))
            {
                if (TryParseHanimeId(info.Name, out var parsedId))
                {
                    id = parsedId;
                    logger.LogInformationIfEnabled($"Parsed Hanime ID from name '{info.Name}': {id}");
                }
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                logger.LogDebugIfEnabled($"No Hanime ID found for {info.Name}");
                return result;
            }

            // Validate configuration
            if (!ConfigurationManager.IsConfigurationValid())
            {
                logger.LogErrorIfEnabled("Invalid plugin configuration");
                return result;
            }

            logger.LogInformationIfEnabled($"Fetching metadata for Hanime ID: {id}");

            using var apiClient = CreateApiClient();
            var metadata = await apiClient.GetMetadataAsync(id, cancellationToken).ConfigureAwait(false);

            if (metadata == null)
            {
                logger.LogWarningIfEnabled($"No Hanime ID found for Hanime ID: {id}");
                return result;
            }

            // Map metadata to Jellyfin entities
            MetadataMapper.MapToMovie(metadata, result.Item, info.Name);
            result.HasMetadata = true;

            logger.LogInformationIfEnabled($"Successfully fetched metadata for Hanime ID: {id}, Title: {result.Item.Name}");
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

            // First priority: Check if there's a Hanime ID in ProviderIds
            string? existingId = null;
            if (searchInfo.ProviderIds != null &&
                searchInfo.ProviderIds.TryGetValue("Hanime", out existingId) &&
                !string.IsNullOrWhiteSpace(existingId))
            {
                logger.LogInformationIfEnabled($"Found Hanime ID in ProviderIds: {existingId}");
                var detailResult = await GetDetailByIdAsync(existingId, cancellationToken).ConfigureAwait(false);
                if (detailResult != null)
                {
                    results.Add(detailResult);
                }

                return results;
            }

            var query = searchInfo.Name;
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            // Second priority: Try to parse the name field as ID
            if (TryParseHanimeId(query, out var hanimeId))
            {
                logger.LogInformationIfEnabled($"Detected Hanime ID in name field: {hanimeId}");
                var detailResult = await GetDetailByIdAsync(hanimeId, cancellationToken).ConfigureAwait(false);
                if (detailResult != null)
                {
                    results.Add(detailResult);
                }

                return results;
            }

            // Third priority: Perform title search
            logger.LogInformationIfEnabled($"Performing title search: {query}");
            return await PerformTitleSearchAsync(query, cancellationToken).ConfigureAwait(false);
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

    private async Task<RemoteSearchResult?> GetDetailByIdAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            using var apiClient = CreateApiClient();
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
                    ProviderIds = { ["Hanime"] = id }
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogErrorIfEnabled($"Failed to get detail for Hanime ID: {id}", ex);
        }

        return null;
    }

    private async Task<IEnumerable<RemoteSearchResult>> PerformTitleSearchAsync(string title, CancellationToken cancellationToken)
    {
        try
        {
            using var apiClient = CreateApiClient();
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
