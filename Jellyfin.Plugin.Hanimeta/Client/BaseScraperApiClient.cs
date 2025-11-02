using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Hanimeta.Extensions;
using Jellyfin.Plugin.Hanimeta.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.Client
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

        // Metrics collection (Problem 15)
        private long totalRequests;
        private long successfulRequests;
        private long failedRequests;
        private long rateLimitedRequests;
        private long totalResponseTimeMs;
        private readonly object metricsLock = new object();

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

            this.httpClient = new HttpClient
            {
                // Increase timeout to accommodate rate limiting and slow scraping operations
                // Default ASP.NET timeout is 60s, but with rate limiting (30s) + scraping (30s) we need more
                Timeout = TimeSpan.FromMinutes(3), // 3 minutes total timeout
            };

            if (!string.IsNullOrWhiteSpace(this.apiToken))
            {
                this.httpClient.DefaultRequestHeaders.Add("X-API-Token", this.apiToken);
            }

            this.logger.LogDebugIfEnabled($"HTTP client initialized with {this.httpClient.Timeout.TotalSeconds}s timeout for {apiPath}");
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
            const int baseDelayMs = 20000; // Start with 20 second
            const int maxDelayMs = 120000; // Cap at 120 seconds
            int attempt = 0;

            var stopwatch = Stopwatch.StartNew();

            while (!cancellationToken.IsCancellationRequested)
            {
                attempt++;
                RecordRequest(); // Problem 15: Track metrics

                try
                {
                    this.logger.LogDebugIfEnabled($"Searching for title: {title} (attempt {attempt})");

                    var url = $"{this.baseUrl}/api/{this.apiPath}/search?title={System.Net.WebUtility.UrlEncode(title)}&max={maxResults}";
                    var response = await this.httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                    // Handle 429 (Too Many Requests) with exponential backoff
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        RecordRateLimited(); // Problem 15: Track rate limiting
                        
                        // Exponential backoff with maximum cap
                        var delay = Math.Min(baseDelayMs * (int)Math.Pow(2, Math.Min(attempt - 1, 10)), maxDelayMs);
                        this.logger.LogWarningIfEnabled($"Rate limited (429) for title: {title}. Retrying in {delay}ms (attempt {attempt})");
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    // For other HTTP errors, throw immediately without retry (Problem 18: Network errors throw)
                    response.EnsureSuccessStatusCode();
                    
                    var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    // Problem 20: Parse JSON once and reuse
                    using var jsonDoc = JsonDocument.Parse(json);
                    var apiResponse = jsonDoc.RootElement;
                    
                    if (!this.IsSuccessResponse(apiResponse, out var data))
                    {
                        // Problem 18: Parse failure returns empty array (not throw)
                        this.logger.LogWarningIfEnabled($"Search API response indicates failure for title: {title}");
                        RecordSuccess(stopwatch.ElapsedMilliseconds); // Still count as "successful" API call
                        return Array.Empty<TSearchResult>();
                    }

                    var results = this.ParseSearchResults(data);
                    RecordSuccess(stopwatch.ElapsedMilliseconds); // Problem 15: Track success
                    return results;
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogDebugIfEnabled($"Search cancelled for title: {title}");
                    RecordFailure(); // Problem 15: Track cancellation as failure
                    throw; // Problem 18: Cancellation throws
                }
                catch (HttpRequestException ex)
                {
                    // Problem 18: Network errors throw (don't retry on network failures)
                    RecordFailure(); // Problem 15: Track failure
                    this.logger.LogErrorIfEnabled($"Search failed for title: {title} due to network error on attempt {attempt}", ex);
                    throw;
                }
                catch (Exception ex)
                {
                    // Problem 18: Other errors also throw
                    RecordFailure(); // Problem 15: Track failure
                    this.logger.LogErrorIfEnabled($"Search failed for title: {title} on attempt {attempt}", ex);
                    throw;
                }
            }

            // Only reach here if cancelled
            RecordFailure(); // Problem 15: Track failure
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
        /// Gets current client metrics/statistics.
        /// </summary>
        /// <returns>Client statistics object.</returns>
        public ClientStatistics GetStatistics()
        {
            lock (metricsLock)
            {
                var avgResponseTime = totalRequests > 0 ? (double)totalResponseTimeMs / totalRequests : 0;
                var successRate = totalRequests > 0 ? (double)successfulRequests / totalRequests : 0;

                return new ClientStatistics
                {
                    TotalRequests = totalRequests,
                    SuccessfulRequests = successfulRequests,
                    FailedRequests = failedRequests,
                    RateLimitedRequests = rateLimitedRequests,
                    AverageResponseTimeMs = avgResponseTime,
                    SuccessRate = successRate
                };
            }
        }

        /// <summary>
        /// Logs current client statistics to the logger.
        /// </summary>
        public void LogStatistics()
        {
            var stats = GetStatistics();
            this.logger.LogInformationIfEnabled(
                $"[{apiPath}] API Client Stats: Total={stats.TotalRequests}, " +
                $"Success={stats.SuccessfulRequests}, Failed={stats.FailedRequests}, " +
                $"RateLimited={stats.RateLimitedRequests}, AvgTime={stats.AverageResponseTimeMs:F0}ms, " +
                $"SuccessRate={stats.SuccessRate:P1}");
        }

        /// <summary>
        /// Releases all resources used by the current instance.
        /// </summary>
        public void Dispose()
        {
            // Log final statistics before disposal
            if (totalRequests > 0)
            {
                LogStatistics();
            }
            
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

        /// <summary>
        /// Records a request attempt for metrics tracking.
        /// </summary>
        private void RecordRequest()
        {
            Interlocked.Increment(ref totalRequests);
        }

        /// <summary>
        /// Records a successful request with response time for metrics tracking.
        /// </summary>
        /// <param name="responseTimeMs">The response time in milliseconds.</param>
        private void RecordSuccess(long responseTimeMs)
        {
            Interlocked.Increment(ref successfulRequests);
            Interlocked.Add(ref totalResponseTimeMs, responseTimeMs);
        }

        /// <summary>
        /// Records a failed request for metrics tracking.
        /// </summary>
        private void RecordFailure()
        {
            Interlocked.Increment(ref failedRequests);
        }

        /// <summary>
        /// Records a rate limited request for metrics tracking.
        /// </summary>
        private void RecordRateLimited()
        {
            Interlocked.Increment(ref rateLimitedRequests);
        }

        /// <summary>
        /// Internal method to fetch metadata with retry logic and metrics tracking.
        /// </summary>
        /// <param name="id">The content ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The metadata if found, otherwise null.</returns>
        private async Task<TMetadata?> GetMetadataInternalAsync(string id, CancellationToken cancellationToken = default)
        {
            const int baseDelayMs = 20000; // Start with 20 second
            const int maxDelayMs = 120000; // Cap at 120 seconds
            int attempt = 0;

            var stopwatch = Stopwatch.StartNew();

            while (!cancellationToken.IsCancellationRequested)
            {
                attempt++;
                RecordRequest(); // Problem 15: Track metrics

                try
                {
                    this.logger.LogDebugIfEnabled($"Fetching metadata for ID: {id} (attempt {attempt})");

                    var requestUrl = $"{this.baseUrl}/api/{this.apiPath}/{id}";
                    var response = await this.httpClient.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);

                    // Handle 429 (Too Many Requests) with exponential backoff
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        RecordRateLimited(); // Problem 15: Track rate limiting
                        
                        // Exponential backoff with maximum cap
                        var delay = Math.Min(baseDelayMs * (int)Math.Pow(2, Math.Min(attempt - 1, 10)), maxDelayMs);
                        this.logger.LogWarningIfEnabled($"Rate limited (429) for ID: {id}. Retrying in {delay}ms (attempt {attempt})");
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    // For other HTTP errors, throw immediately without retry (Problem 18: Network errors throw)
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    this.logger.LogDebugIfEnabled($"Received response: {json}");

                    // Problem 20: Parse JSON once and reuse
                    using var jsonDoc = JsonDocument.Parse(json);
                    var apiResponse = jsonDoc.RootElement;
                    
                    if (!this.IsSuccessResponse(apiResponse, out var data))
                    {
                        // Problem 18: Parse failure returns null (not throw)
                        this.logger.LogWarningIfEnabled($"API response indicates failure for ID: {id}");
                        RecordSuccess(stopwatch.ElapsedMilliseconds); // Still count as "successful" API call
                        return null;
                    }

                    var metadata = this.ParseMetadata(data, id);
                    RecordSuccess(stopwatch.ElapsedMilliseconds); // Problem 15: Track success
                    return metadata;
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogDebugIfEnabled($"Get metadata cancelled for ID: {id}");
                    RecordFailure(); // Problem 15: Track cancellation as failure
                    throw; // Problem 18: Cancellation throws
                }
                catch (HttpRequestException ex)
                {
                    // Problem 18: Network errors throw (don't retry on network failures)
                    RecordFailure(); // Problem 15: Track failure
                    this.logger.LogErrorIfEnabled($"Get metadata failed for ID: {id} due to network error on attempt {attempt}", ex);
                    throw;
                }
                catch (JsonException ex)
                {
                    // Problem 18: JSON parse errors return null (data corruption, not network issue)
                    RecordFailure(); // Problem 15: Track failure
                    this.logger.LogErrorIfEnabled($"Failed to parse JSON response for ID: {id}", ex);
                    return null;
                }
                catch (Exception ex)
                {
                    // Problem 18: Other errors throw
                    RecordFailure(); // Problem 15: Track failure
                    this.logger.LogErrorIfEnabled($"Get metadata failed for ID: {id} on attempt {attempt}", ex);
                    throw;
                }
            }

            // Only reach here if cancelled
            RecordFailure(); // Problem 15: Track failure
            this.logger.LogDebugIfEnabled($"Get metadata cancelled for ID: {id} after {attempt} attempts");
            return null;
        }
    }

    /// <summary>
    /// Client statistics for monitoring and diagnostics.
    /// </summary>
    public class ClientStatistics
    {
        /// <summary>
        /// Gets or sets the total number of API requests made.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of successful API requests.
        /// </summary>
        public long SuccessfulRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of failed API requests.
        /// </summary>
        public long FailedRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of rate limited API requests (HTTP 429).
        /// </summary>
        public long RateLimitedRequests { get; set; }

        /// <summary>
        /// Gets or sets the average response time in milliseconds.
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the success rate as a decimal (0.0 to 1.0).
        /// </summary>
        public double SuccessRate { get; set; }
    }
}
