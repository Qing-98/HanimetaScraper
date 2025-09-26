using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DLsiteScraper.Client;
using Jellyfin.Plugin.DLsiteScraper.Configuration;
using Jellyfin.Plugin.DLsiteScraper.Extensions;
using Jellyfin.Plugin.DLsiteScraper.Helpers;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DLsiteScraper.Providers;

/// <summary>
/// DLsite metadata provider that interacts with backend service.
/// </summary>
public class DLsiteMetadataProvider
    : IRemoteMetadataProvider<Movie, MovieInfo>, IRemoteSearchProvider<MovieInfo>, IHasOrder
{
    private readonly ILogger<DLsiteMetadataProvider> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DLsiteMetadataProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public DLsiteMetadataProvider(ILogger<DLsiteMetadataProvider> logger)
    {
        this.logger = logger;
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

            // Try to get DLsite ID from provider IDs
            string? id = null;
            if (info.ProviderIds != null &&
                info.ProviderIds.TryGetValue("DLsite", out id) &&
                !string.IsNullOrWhiteSpace(id))
            {
                // ID found in provider IDs
            }

            // If no ID found, try to parse from the name
            if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(info.Name))
            {
                if (TryParseDLsiteId(info.Name, out var parsedId))
                {
                    id = parsedId;
                    logger.LogInformationIfEnabled($"Parsed DLsite ID from name '{info.Name}': {id}");
                }
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                logger.LogDebugIfEnabled($"No DLsite ID found for {info.Name}");
                return result;
            }

            // Validate configuration
            if (!ConfigurationManager.IsConfigurationValid())
            {
                logger.LogErrorIfEnabled("Invalid plugin configuration");
                return result;
            }

            logger.LogInformationIfEnabled($"Fetching metadata for DLsite ID: {id}");

            using var apiClient = CreateApiClient();
            var metadata = await apiClient.GetMetadataAsync(id, cancellationToken).ConfigureAwait(false);

            if (metadata == null)
            {
                logger.LogWarningIfEnabled($"No metadata found for DLsite ID: {id}");
                return result;
            }

            // Map metadata to Jellyfin entities
            MetadataMapper.MapToMovie(metadata, result.Item, info.Name);
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

            var query = searchInfo.Name;
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            // Second priority: Try to parse the name field as ID
            if (TryParseDLsiteId(query, out var dlsiteId))
            {
                logger.LogInformationIfEnabled($"Detected DLsite ID in name field: {dlsiteId}");
                var detailResult = await GetDetailByIdAsync(dlsiteId, cancellationToken).ConfigureAwait(false);
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

    private DLsiteApiClient CreateApiClient()
    {
        return new DLsiteApiClient(
            logger as ILogger<DLsiteApiClient> ??
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DLsiteApiClient>.Instance,
            ConfigurationManager.GetBackendUrl(),
            ConfigurationManager.GetApiToken(),
            ConfigurationManager.IsLoggingEnabled());
    }
}
