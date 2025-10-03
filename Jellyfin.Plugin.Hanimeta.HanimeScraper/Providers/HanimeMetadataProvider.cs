using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Hanimeta.Common.Extensions;
using Jellyfin.Plugin.Hanimeta.Common.Helpers;
using Jellyfin.Plugin.Hanimeta.HanimeScraper.Client;
using Jellyfin.Plugin.Hanimeta.HanimeScraper.Helpers;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.HanimeScraper.Providers;

/// <summary>
/// Hanime metadata provider that interacts with backend service.
/// </summary>
public class HanimeMetadataProvider
    : IRemoteMetadataProvider<Movie, MovieInfo>, IRemoteSearchProvider<MovieInfo>, IHasOrder
{
    private readonly ILogger<HanimeMetadataProvider> logger;
    private readonly Func<HanimeApiClient> apiClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="HanimeMetadataProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="apiClientFactory">Factory to create API client instances.</param>
    public HanimeMetadataProvider(ILogger<HanimeMetadataProvider> logger, Func<HanimeApiClient> apiClientFactory)
    {
        this.logger = logger;
        this.apiClientFactory = apiClientFactory;
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
            this.logger.LogInformationIfEnabled(
                $"GetMetadata called with MovieInfo: Name={info?.Name}, ProviderIds={string.Join(", ", info?.ProviderIds?.Select(p => $"{p.Key}:{p.Value}") ?? Array.Empty<string>())}");

            var result = new MetadataResult<Movie>
            {
                Item = new Movie(),
                HasMetadata = false,
            };

            if (info == null)
            {
                this.logger.LogErrorIfEnabled("MovieInfo is null");
                return result;
            }

            // Pre-clean filenames for better parsing using common FilenameCleaner
            var cleanedName = FilenameCleaner.Clean(info.Name);
            var cleanedOriginal = FilenameCleaner.Clean(info.OriginalTitle);

            // Try to get Hanime ID from provider IDs
            string? id = null;
            if (info.ProviderIds != null &&
                info.ProviderIds.TryGetValue("Hanime", out id) &&
                !string.IsNullOrWhiteSpace(id))
            {
                // ID found in provider IDs
            }

            // If no ID found, try to parse from the cleaned name
            if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(cleanedName))
            {
                if (this.TryParseHanimeId(cleanedName, out var parsedId))
                {
                    id = parsedId;
                    this.logger.LogInformationIfEnabled($"Parsed Hanime ID from name '{cleanedName}': {id}");
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
                    this.logger.LogInformationIfEnabled($"No Hanime ID found — performing title search: {query}");
                    var searchResults = await this.PerformTitleSearchAsync(query, cancellationToken).ConfigureAwait(false);
                    var first = searchResults?.FirstOrDefault();
                    if (first != null && first.ProviderIds != null && first.ProviderIds.TryGetValue("Hanime", out var foundId) && !string.IsNullOrWhiteSpace(foundId))
                    {
                        id = foundId;
                        this.logger.LogInformationIfEnabled($"Title search matched Hanime ID: {id}");
                    }
                }

                if (string.IsNullOrWhiteSpace(id))
                {
                    this.logger.LogDebugIfEnabled($"No Hanime ID found for {info.Name}");
                    return result;
                }
            }

            // Validate configuration
            if (!Plugin.IsConfigurationValid())
            {
                this.logger.LogErrorIfEnabled("Invalid plugin configuration");
                return result;
            }

            this.logger.LogInformationIfEnabled($"Fetching metadata for Hanime ID: {id}");

            using var apiClient = this.apiClientFactory();
            var metadata = await apiClient.GetMetadataAsync(id, cancellationToken).ConfigureAwait(false);

            if (metadata == null)
            {
                this.logger.LogWarningIfEnabled($"No Hanime ID found for Hanime ID: {id}");
                return result;
            }

            // Map metadata to Jellyfin entities
            MetadataMapper.MapToMovie(metadata, result.Item, info.Name);

            // Add people into MetadataResult so Jellyfin core handles them (AddPerson handles initialization/dup)
            foreach (var person in MetadataMapper.CreatePersonInfos(metadata))
            {
                try
                {
                    result.AddPerson(person);
                }
                catch (Exception ex)
                {
                    this.logger.LogDebugIfEnabled($"Failed to add person {person?.Name}: {ex.Message}");
                }
            }

            result.HasMetadata = true;

            this.logger.LogInformationIfEnabled($"Successfully fetched metadata for Hanime ID: {id}, Title: {result.Item.Name}");
            return result;
        }
        catch (Exception ex)
        {
            this.logger.LogErrorIfEnabled("Unexpected error in GetMetadata", ex);
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
            this.logger.LogInformationIfEnabled(
                $"GetSearchResults called with: Name='{searchInfo.Name}', Year={searchInfo.Year}, ProviderIds={string.Join(", ", searchInfo.ProviderIds.Select(p => $"{p.Key}:{p.Value}"))}");

            var results = new List<RemoteSearchResult>();

            // First priority: Check if there's a Hanime ID in ProviderIds
            string? existingId = null;
            if (searchInfo.ProviderIds != null &&
                searchInfo.ProviderIds.TryGetValue("Hanime", out existingId) &&
                !string.IsNullOrWhiteSpace(existingId))
            {
                this.logger.LogInformationIfEnabled($"Found Hanime ID in ProviderIds: {existingId}");
                var detailResult = await this.GetDetailByIdAsync(existingId, cancellationToken).ConfigureAwait(false);
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

            // Clean the query to remove noise before searching using common FilenameCleaner
            var cleanedQuery = FilenameCleaner.Clean(query);
            if (string.IsNullOrWhiteSpace(cleanedQuery))
            {
                return results;
            }

            // Perform title search using cleaned query
            this.logger.LogInformationIfEnabled($"Performing title search: {cleanedQuery}");
            return await this.PerformTitleSearchAsync(cleanedQuery, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogErrorIfEnabled("Unexpected error in GetSearchResults", ex);
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
    /// Attempts to parse a Hanime ID from various input formats.
    /// </summary>
    /// <param name="input">The input string to parse.</param>
    /// <param name="id">The extracted Hanime ID if parsing succeeds.</param>
    /// <returns>True if a valid ID was extracted, false otherwise.</returns>
    private bool TryParseHanimeId(string? input, out string id)
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
            using var apiClient = this.apiClientFactory();
            var metadata = await apiClient.GetMetadataAsync(id, cancellationToken).ConfigureAwait(false);

            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Title))
            {
                this.logger.LogInformationIfEnabled($"Found content by ID {id}: {metadata.Title}");
                return new RemoteSearchResult
                {
                    Name = metadata.Title,
                    Overview = metadata.Description,
                    ProductionYear = metadata.Year,
                    ImageUrl = metadata.Primary,
                    ProviderIds = { ["Hanime"] = id },
                };
            }
        }
        catch (Exception ex)
        {
            this.logger.LogErrorIfEnabled($"Failed to get detail for Hanime ID: {id}", ex);
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
            this.logger.LogInformationIfEnabled($"Title search returned {results.Count} results for: {title}");

            return results;
        }
        catch (Exception ex)
        {
            this.logger.LogErrorIfEnabled($"Failed to perform title search for: {title}", ex);
            return Array.Empty<RemoteSearchResult>();
        }
    }
}
