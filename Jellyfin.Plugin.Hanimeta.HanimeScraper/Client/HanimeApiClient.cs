using System;
using System.Collections.Generic;
using System.Text.Json;
using Jellyfin.Plugin.Hanimeta.Common.Client;
using Jellyfin.Plugin.Hanimeta.Common.Extensions;
using Jellyfin.Plugin.Hanimeta.Common.Models;
using Jellyfin.Plugin.Hanimeta.HanimeScraper.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.HanimeScraper.Client;

/// <summary>
/// HTTP client for communicating with the Hanime scraper backend service.
/// </summary>
public class HanimeApiClient : BaseScraperApiClient<HanimeMetadata, BaseSearchResult>
{
    private readonly ILogger<HanimeApiClient> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HanimeApiClient"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="baseUrl">The base URL of the backend service.</param>
    /// <param name="apiToken">The optional API token for authentication.</param>
    public HanimeApiClient(ILogger<HanimeApiClient> logger, string baseUrl, string? apiToken = null)
        : base(logger, baseUrl, "hanime", apiToken)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Parse metadata from JSON data.
    /// </summary>
    /// <param name="data">The JSON data.</param>
    /// <param name="id">The content ID.</param>
    /// <returns>The parsed metadata.</returns>
    protected override HanimeMetadata ParseMetadata(JsonElement data, string id)
    {
        var metadata = new HanimeMetadata();

        // Use base class method to parse common fields
        this.ParseBaseMetadata(data, id, metadata);

        // Parse Hanime-specific people data
        metadata.People = this.ParsePeople(data);

        return metadata;
    }

    /// <summary>
    /// Parse search results from JSON data.
    /// </summary>
    /// <param name="data">The JSON data.</param>
    /// <returns>Collection of search results.</returns>
    protected override IEnumerable<BaseSearchResult> ParseSearchResults(JsonElement data)
    {
        var results = new List<BaseSearchResult>();

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var result = this.ParseSearchResult(item);
                if (result != null)
                {
                    results.Add(result);
                }
            }
        }
        else if (data.ValueKind == JsonValueKind.Object)
        {
            var result = this.ParseSearchResult(data);
            if (result != null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    private BaseSearchResult? ParseSearchResult(JsonElement item)
    {
        var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var result = new BaseSearchResult();

        // Use base class method to parse common fields
        this.ParseBaseSearchResult(item, result);

        // Add debug logging for image URL
        if (string.IsNullOrWhiteSpace(result.Primary))
        {
            this.logger.LogWarningIfEnabled($"Hanime search result missing primary image: ID={id}, Title={title}");
        }
        else
        {
            this.logger.LogDebugIfEnabled($"Hanime search result with image: ID={id}, Title={title}, Primary={result.Primary}");
        }

        return result;
    }

    private HanimePerson[] ParsePeople(JsonElement data)
    {
        if (!data.TryGetProperty("people", out var peopleProp) || peopleProp.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<HanimePerson>();
        }

        var people = new List<HanimePerson>();
        foreach (var person in peopleProp.EnumerateArray())
        {
            var name = person.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                people.Add(new HanimePerson
                {
                    Name = name,
                    Type = person.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null,
                    Role = person.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null
                });
            }
        }

        return people.ToArray();
    }
}
