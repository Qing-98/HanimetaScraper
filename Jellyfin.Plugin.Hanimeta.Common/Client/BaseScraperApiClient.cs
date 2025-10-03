using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Hanimeta.Common.Extensions;
using Jellyfin.Plugin.Hanimeta.Common.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.Common.Client
{
    /// <summary>
    /// Base HTTP client for communicating with scraper backend services.
    /// </summary>
    /// <typeparam name="TMetadata">The metadata type.</typeparam>
    /// <typeparam name="TSearchResult">The search result type.</typeparam>
    public abstract class BaseScraperApiClient<TMetadata, TSearchResult> : IDisposable
        where TMetadata : BaseMetadata
        where TSearchResult : BaseSearchResult
    {
        private readonly HttpClient httpClient;
        private readonly ILogger logger;
        private readonly string baseUrl;
        private readonly string? apiToken;
        private readonly string apiPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseScraperApiClient{TMetadata, TSearchResult}"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="baseUrl">The base URL of the backend service.</param>
        /// <param name="apiPath">The API path segment (e.g., "hanime" or "dlsite").</param>
        /// <param name="apiToken">The optional API token for authentication.</param>
        protected BaseScraperApiClient(ILogger logger, string baseUrl, string apiPath, string? apiToken = null)
        {
            this.logger = logger;
            this.baseUrl = baseUrl.TrimEnd('/');
            this.apiPath = apiPath;
            this.apiToken = apiToken;

            this.httpClient = new HttpClient();
            if (!string.IsNullOrWhiteSpace(this.apiToken))
            {
                this.httpClient.DefaultRequestHeaders.Add("X-API-Token", this.apiToken);
            }
        }

        /// <summary>
        /// Gets metadata for a specific ID.
        /// </summary>
        /// <param name="id">The content ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The metadata if found.</returns>
        public async Task<TMetadata?> GetMetadataAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            // Directly fetch metadata without in-flight request deduplication
            return await this.GetMetadataInternalAsync(id, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Searches for content by title.
        /// </summary>
        /// <param name="title">The search title.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Collection of search results.</returns>
        public async Task<IEnumerable<TSearchResult>> SearchAsync(string title, int maxResults = 10, CancellationToken cancellationToken = default)
        {
            const int baseDelayMs = 1000; // Start with 1 second
            const int maxDelayMs = 30000; // Cap at 30 seconds
            int attempt = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                attempt++;
                try
                {
                    this.logger.LogDebugIfEnabled($"Searching for title: {title} (attempt {attempt})");

                    var url = $"{this.baseUrl}/api/{this.apiPath}/search?title={System.Net.WebUtility.UrlEncode(title)}&max={maxResults}";
                    var response = await this.httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                    // Handle 429 (Too Many Requests) with exponential backoff
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        // Exponential backoff with maximum cap
                        var delay = Math.Min(baseDelayMs * (int)Math.Pow(2, Math.Min(attempt - 1, 10)), maxDelayMs);
                        this.logger.LogWarningIfEnabled($"Rate limited (429) for title: {title}. Retrying in {delay}ms (attempt {attempt})");
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    // For other HTTP errors, throw immediately without retry
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    var apiResponse = JsonDocument.Parse(json).RootElement;
                    if (!this.IsSuccessResponse(apiResponse, out var data))
                    {
                        this.logger.LogWarningIfEnabled($"Search API response indicates failure for title: {title}");
                        return Array.Empty<TSearchResult>();
                    }

                    return this.ParseSearchResults(data);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogDebugIfEnabled($"Search cancelled for title: {title}");
                    throw;
                }
                catch (Exception ex)
                {
                    // For non-429 errors, throw immediately without retry
                    this.logger.LogErrorIfEnabled($"Search failed for title: {title} on attempt {attempt}", ex);
                    throw;
                }
            }

            // Only reach here if cancelled
            this.logger.LogDebugIfEnabled($"Search cancelled for title: {title} after {attempt} attempts");
            return Array.Empty<TSearchResult>();
        }

        /// <summary>
        /// Gets an HTTP response for downloading images.
        /// </summary>
        /// <param name="url">The image URL.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP response message.</returns>
        public Task<HttpResponseMessage> GetImageResponseAsync(string url, CancellationToken cancellationToken = default)
        {
            return this.httpClient.GetAsync(url, cancellationToken);
        }

        /// <summary>
        /// Releases all resources used by the current instance.
        /// </summary>
        public void Dispose()
        {
            this.httpClient?.Dispose();
        }

        /// <summary>
        /// Determines if the API response was successful and extracts the data.
        /// </summary>
        /// <param name="apiResponse">The API response JSON element.</param>
        /// <param name="data">The extracted data, if successful.</param>
        /// <returns>True if the response indicates success; otherwise, false.</returns>
        protected bool IsSuccessResponse(JsonElement apiResponse, out JsonElement data)
        {
            data = default;
            return apiResponse.TryGetProperty("success", out var successProp) &&
                   successProp.GetBoolean() &&
                   apiResponse.TryGetProperty("data", out data);
        }

        /// <summary>
        /// Parse string array from JSON data.
        /// </summary>
        /// <param name="data">The JSON data.</param>
        /// <param name="propertyName">The property name of the array.</param>
        /// <returns>Array of string values.</returns>
        protected string[] ParseStringArray(JsonElement data, string propertyName)
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

        /// <summary>
        /// Parses basic metadata fields that are common across all providers.
        /// </summary>
        /// <param name="data">The JSON data.</param>
        /// <param name="id">The content ID.</param>
        /// <param name="metadata">The metadata instance to populate.</param>
        protected void ParseBaseMetadata(JsonElement data, string id, TMetadata metadata)
        {
            metadata.Id = id;
            metadata.Title = data.TryGetProperty("title", out var title) ? title.GetString() : null;
            metadata.OriginalTitle = data.TryGetProperty("originalTitle", out var originalTitle) ? originalTitle.GetString() : null;
            metadata.Description = data.TryGetProperty("description", out var desc) ? desc.GetString() : null;
            metadata.Year = data.TryGetProperty("year", out var year) && year.TryGetInt32(out var y) ? y : null;
            metadata.Rating = data.TryGetProperty("rating", out var rating) && rating.ValueKind == JsonValueKind.Number ? (float)rating.GetDouble() : null;
            metadata.ReleaseDate = data.TryGetProperty("releaseDate", out var releaseDate) && DateTimeOffset.TryParse(releaseDate.GetString(), out var rd) ? rd.UtcDateTime : null;
            metadata.Primary = data.TryGetProperty("primary", out var primary) ? primary.GetString() : null;
            metadata.Tags = this.ParseStringArray(data, "tags");
            metadata.Genres = this.ParseStringArray(data, "genres"); // Keep genres parsing for future compatibility
            metadata.Studios = this.ParseStringArray(data, "studios");
            metadata.Series = this.ParseStringArray(data, "series");
            metadata.SourceUrls = this.ParseStringArray(data, "sourceUrls");
        }

        /// <summary>
        /// Parses basic search result fields that are common across all providers.
        /// </summary>
        /// <param name="item">The JSON item element.</param>
        /// <param name="searchResult">The search result instance to populate.</param>
        protected void ParseBaseSearchResult(JsonElement item, TSearchResult searchResult)
        {
            searchResult.Id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            searchResult.Title = item.TryGetProperty("title", out var title) ? title.GetString() : null;
            searchResult.OriginalTitle = item.TryGetProperty("originalTitle", out var originalTitle) ? originalTitle.GetString() : null;
            searchResult.Description = item.TryGetProperty("description", out var desc) ? desc.GetString() : null;
            searchResult.Year = item.TryGetProperty("year", out var year) && year.TryGetInt32(out var y) ? y : null;
            searchResult.Primary = item.TryGetProperty("primary", out var primary) ? primary.GetString() : null;
        }

        /// <summary>
        /// Parse metadata from JSON data.
        /// </summary>
        /// <param name="data">The JSON data.</param>
        /// <param name="id">The content ID.</param>
        /// <returns>The parsed metadata.</returns>
        protected abstract TMetadata ParseMetadata(JsonElement data, string id);

        /// <summary>
        /// Parse search results from JSON data.
        /// </summary>
        /// <param name="data">The JSON data.</param>
        /// <returns>Collection of search results.</returns>
        protected abstract IEnumerable<TSearchResult> ParseSearchResults(JsonElement data);

        // Original metadata fetching logic extracted to internal method
        private async Task<TMetadata?> GetMetadataInternalAsync(string id, CancellationToken cancellationToken = default)
        {
            const int baseDelayMs = 3000; // Start with 3 second
            const int maxDelayMs = 100000; // Cap at 100 seconds
            int attempt = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                attempt++;
                try
                {
                    this.logger.LogDebugIfEnabled($"Fetching metadata for ID: {id} (attempt {attempt})");

                    var requestUrl = $"{this.baseUrl}/api/{this.apiPath}/{id}";
                    var response = await this.httpClient.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);

                    // Handle 429 (Too Many Requests) with exponential backoff
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        // Exponential backoff with maximum cap
                        var delay = Math.Min(baseDelayMs * (int)Math.Pow(2, Math.Min(attempt - 1, 10)), maxDelayMs);
                        this.logger.LogWarningIfEnabled($"Rate limited (429) for ID: {id}. Retrying in {delay}ms (attempt {attempt})");
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    // For other HTTP errors, throw immediately without retry
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    this.logger.LogDebugIfEnabled($"Received response: {json}");

                    var apiResponse = JsonDocument.Parse(json).RootElement;
                    if (!this.IsSuccessResponse(apiResponse, out var data))
                    {
                        this.logger.LogWarningIfEnabled($"API response indicates failure for ID: {id}");
                        return null;
                    }

                    return this.ParseMetadata(data, id);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogDebugIfEnabled($"Get metadata cancelled for ID: {id}");
                    throw;
                }
                catch (Exception ex)
                {
                    // For non-429 errors, throw immediately without retry
                    this.logger.LogErrorIfEnabled($"Get metadata failed for ID: {id} on attempt {attempt}", ex);
                    throw;
                }
            }

            // Only reach here if cancelled
            this.logger.LogDebugIfEnabled($"Get metadata cancelled for ID: {id} after {attempt} attempts");
            return null;
        }
    }
}
