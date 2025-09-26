using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HanimeScraper.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HanimeScraper.Client;

/// <summary>
/// HTTP client for communicating with the Hanime scraper backend service.
/// </summary>
public class HanimeApiClient : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly ILogger<HanimeApiClient> logger;
    private readonly string baseUrl;
    private readonly string? apiToken;
    private readonly bool enableLogging;

    /// <summary>
    /// Initializes a new instance of the <see cref="HanimeApiClient"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="baseUrl">The base URL of the backend service.</param>
    /// <param name="apiToken">The optional API token for authentication.</param>
    /// <param name="enableLogging">Whether logging is enabled.</param>
    public HanimeApiClient(ILogger<HanimeApiClient> logger, string baseUrl, string? apiToken = null, bool enableLogging = true)
    {
        this.logger = logger;
        this.baseUrl = baseUrl.TrimEnd('/');
        this.apiToken = apiToken;
        this.enableLogging = enableLogging;

        httpClient = new HttpClient();
        if (!string.IsNullOrWhiteSpace(this.apiToken))
        {
            httpClient.DefaultRequestHeaders.Add("X-API-Token", this.apiToken);
        }
    }

    /// <summary>
    /// Gets metadata for a specific Hanime ID.
    /// </summary>
    /// <param name="id">The Hanime ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Hanime metadata if found.</returns>
    public async Task<HanimeMetadata?> GetMetadataAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            LogDebug($"Fetching metadata for Hanime ID: {id}");

            var requestUrl = $"{baseUrl}/api/hanime/{id}";
            var response = await httpClient.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            LogDebug($"Received response: {json}");

            var apiResponse = JsonDocument.Parse(json).RootElement;
            if (!IsSuccessResponse(apiResponse, out var data))
            {
                LogWarning($"API response indicates failure for ID: {id}");
                return null;
            }

            return ParseMetadata(data, id);
        }
        catch (Exception ex)
        {
            LogError($"Failed to fetch metadata for ID: {id}", ex);
            return null;
        }
    }

    /// <summary>
    /// Searches for Hanime content by title.
    /// </summary>
    /// <param name="title">The search title.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of search results.</returns>
    public async Task<System.Collections.Generic.IEnumerable<HanimeSearchResult>> SearchAsync(string title, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            LogDebug($"Searching for title: {title}");

            var url = $"{baseUrl}/api/hanime/search?title={System.Net.WebUtility.UrlEncode(title)}&max={maxResults}";
            var json = await httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);

            var apiResponse = JsonDocument.Parse(json).RootElement;
            if (!IsSuccessResponse(apiResponse, out var data))
            {
                LogWarning($"Search API response indicates failure for title: {title}");
                return Array.Empty<HanimeSearchResult>();
            }

            return ParseSearchResults(data);
        }
        catch (Exception ex)
        {
            LogError($"Failed to search for title: {title}", ex);
            return Array.Empty<HanimeSearchResult>();
        }
    }

    /// <summary>
    /// Gets an HTTP response for downloading images.
    /// </summary>
    /// <param name="url">The image URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP response message.</returns>
    public Task<HttpResponseMessage> GetImageResponseAsync(string url, CancellationToken cancellationToken = default)
    {
        return httpClient.GetAsync(url, cancellationToken);
    }

    /// <summary>
    /// Releases all resources used by the current instance.
    /// </summary>
    public void Dispose()
    {
        httpClient?.Dispose();
    }

    private bool IsSuccessResponse(JsonElement apiResponse, out JsonElement data)
    {
        data = default;
        return apiResponse.TryGetProperty("success", out var successProp) &&
               successProp.GetBoolean() &&
               apiResponse.TryGetProperty("data", out data);
    }

    private HanimeMetadata ParseMetadata(JsonElement data, string id)
    {
        var metadata = new HanimeMetadata
        {
            Id = id,
            Title = data.TryGetProperty("title", out var title) ? title.GetString() : null,
            OriginalTitle = data.TryGetProperty("originalTitle", out var originalTitle) ? originalTitle.GetString() : null,
            Description = data.TryGetProperty("description", out var desc) ? desc.GetString() : null,
            Year = data.TryGetProperty("year", out var year) && year.TryGetInt32(out var y) ? y : null,
            Rating = data.TryGetProperty("rating", out var rating) && rating.ValueKind == JsonValueKind.Number ? (float)rating.GetDouble() : null,
            ReleaseDate = data.TryGetProperty("releaseDate", out var releaseDate) && DateTimeOffset.TryParse(releaseDate.GetString(), out var rd) ? rd.UtcDateTime : null,
            Primary = data.TryGetProperty("primary", out var primary) ? primary.GetString() : null,
            Genres = ParseStringArray(data, "genres"),
            Studios = ParseStringArray(data, "studios"),
            Series = ParseStringArray(data, "series"),
            People = ParsePeople(data)
        };

        return metadata;
    }

    private System.Collections.Generic.IEnumerable<HanimeSearchResult> ParseSearchResults(JsonElement data)
    {
        var results = new List<HanimeSearchResult>();

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var result = ParseSearchResult(item);
                if (result != null)
                {
                    results.Add(result);
                }
            }
        }
        else if (data.ValueKind == JsonValueKind.Object)
        {
            var result = ParseSearchResult(data);
            if (result != null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    private HanimeSearchResult? ParseSearchResult(JsonElement item)
    {
        var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var primary = item.TryGetProperty("primary", out var primaryProp) ? primaryProp.GetString() : null;

        // Add debug logging for image URL
        if (enableLogging)
        {
            if (string.IsNullOrWhiteSpace(primary))
            {
                LogWarning($"Hanime search result missing primary image: ID={id}, Title={title}");
            }
            else
            {
                LogDebug($"Hanime search result with image: ID={id}, Title={title}, Primary={primary}");
            }
        }

        return new HanimeSearchResult
        {
            Id = id,
            Title = title,
            Description = item.TryGetProperty("description", out var desc) ? desc.GetString() : null,
            Year = item.TryGetProperty("year", out var year) && year.TryGetInt32(out var y) ? y : null,
            Primary = primary
        };
    }

    private string[] ParseStringArray(JsonElement data, string propertyName)
    {
        if (!data.TryGetProperty(propertyName, out var arrayProp) || arrayProp.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var items = new List<string>();
        foreach (var element in arrayProp.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    items.Add(value);
                }
            }
        }

        return items.ToArray();
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

    private void LogDebug(string message)
    {
        if (enableLogging)
        {
            logger.LogDebug("{Message}", message);
        }
    }

    private void LogWarning(string message)
    {
        if (enableLogging)
        {
            logger.LogWarning("{Message}", message);
        }
    }

    private void LogError(string message, Exception? ex = null)
    {
        if (enableLogging)
        {
            logger.LogError(ex, "{Message}", message);
        }
    }
}
