using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.DLsiteScraper;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DLsiteScraper.Providers;

/// <summary>
/// DLsite metadata provider that interacts with backend service without page parsing.
/// This provider handles metadata extraction and search functionality for DLsite content
/// by communicating with the scraper backend service via HTTP API calls.
/// </summary>
/// <remarks>
/// This provider supports:
/// - Metadata extraction by DLsite product ID (RJ/VJ format)
/// - Search functionality with text queries (supports Japanese)
/// - External ID management and parsing
/// - Image URL extraction from product pages
/// - Personnel information mapping (voice actors, directors, etc.)
/// </remarks>
/// <example>
/// Basic usage in Jellyfin:
/// <code>
/// // The provider is automatically registered via dependency injection
/// // Search for content: "恋爱"
/// // Get metadata by ID: "RJ01402281"
/// </code>
/// </example>
public class DLsiteMetadataProvider
    : IRemoteMetadataProvider<Movie, MovieInfo>, IRemoteSearchProvider<MovieInfo>, IHasOrder
{
    private readonly ILogger<DLsiteMetadataProvider> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DLsiteMetadataProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for tracking operations.</param>
    public DLsiteMetadataProvider(ILogger<DLsiteMetadataProvider> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    /// <summary>
    /// Gets the name of this metadata provider.
    /// </summary>
    public string Name => "DLsite";

    /// <inheritdoc />
    /// <summary>
    /// Gets the execution order for this provider (0 = same priority as Hanime).
    /// </summary>
    public int Order => 0;

    /// <summary>
    /// Logs error messages if logging is enabled in plugin configuration.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="ex">Optional exception to include in the log.</param>
    private void LogError(string message, Exception? ex = null)
    {
        // Defensive check for null configuration
        if (Plugin.PluginConfig?.EnableLogging != true)
        {
            return;
        }

        try
        {
            logger.LogError(ex, "{Message}", message);
        }
        catch
        {
            // Suppress logging errors to prevent cascading failures
        }
    }

    /// <summary>
    /// Logs informational messages if logging is enabled in plugin configuration.
    /// </summary>
    /// <param name="message">The information message to log.</param>
    private void LogInformation(string message)
    {
        // Defensive check for null configuration
        if (Plugin.PluginConfig?.EnableLogging != true)
        {
            return;
        }

        try
        {
            logger.LogInformation("{Message}", message);
        }
        catch
        {
            // Suppress logging errors to prevent cascading failures
        }
    }

    /// <summary>
    /// Logs debug messages if logging is enabled in plugin configuration.
    /// </summary>
    /// <param name="message">The debug message to log.</param>
    private void LogDebug(string message)
    {
        // Defensive check for null configuration
        if (Plugin.PluginConfig?.EnableLogging != true)
        {
            return;
        }

        try
        {
            logger.LogDebug("{Message}", message);
        }
        catch
        {
            // Suppress logging errors to prevent cascading failures
        }
    }

    /// <summary>
    /// Logs warning messages if logging is enabled in plugin configuration.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    private void LogWarning(string message)
    {
        // Defensive check for null configuration
        if (Plugin.PluginConfig?.EnableLogging != true)
        {
            return;
        }

        try
        {
            logger.LogWarning("{Message}", message);
        }
        catch
        {
            // Suppress logging errors to prevent cascading failures
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// Gets metadata for a movie item using the DLsite provider ID.
    /// This method fetches detailed metadata from the backend service and maps it to Jellyfin's movie entity.
    /// </summary>
    /// <param name="info">Movie information containing the DLsite provider ID and other metadata.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A metadata result containing the populated movie entity.</returns>
    /// <example>
    /// Test case - Valid DLsite ID:
    /// <code>
    /// var movieInfo = new MovieInfo
    /// {
    ///     Name = "Test Product",
    ///     ProviderIds = { ["DLsite"] = "RJ01402281" }
    /// };
    /// var result = await provider.GetMetadata(movieInfo, CancellationToken.None);
    /// Assert.True(result.HasMetadata);
    /// Assert.NotNull(result.Item);
    /// Assert.Equal("RJ01402281", result.Item.GetProviderId("DLsite"));
    /// </code>
    ///
    /// Test case - Missing DLsite ID:
    /// <code>
    /// var movieInfo = new MovieInfo { Name = "Test Product" };
    /// var result = await provider.GetMetadata(movieInfo, CancellationToken.None);
    /// Assert.False(result.HasMetadata);
    /// </code>
    /// </example>
    public async Task<MetadataResult<Movie>> GetMetadata(
        MovieInfo info,
        CancellationToken cancellationToken)
    {
        try
        {
            LogInformation(
                $"GetMetadata called with MovieInfo: Name={info?.Name}, ProviderIds={string.Join(", ", info?.ProviderIds?.Select(p => $"{p.Key}:{p.Value}") ?? Array.Empty<string>())}");

            var result = new MetadataResult<Movie> { Item = new Movie(), HasMetadata = false };
            result.People = new List<PersonInfo>();

            // Safety check
            if (info == null)
            {
                LogError("MovieInfo is null");
                return result;
            }

            // Try to get DLsite ID from provider IDs
            var id = info.GetProviderId("DLsite");
            LogDebug($"[DEBUG] GetMetadata: Retrieved DLsite ID from ProviderIds: '{id}'");

            // If no ID found in provider IDs, try to parse from the name
            if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(info.Name))
            {
                LogDebug($"[DEBUG] GetMetadata: No ID in ProviderIds, attempting to parse from name: '{info.Name}'");
                if (TryParseDLsiteId(info.Name, out var parsedId))
                {
                    id = parsedId;
                    LogInformation($"Parsed DLsite ID from name '{info.Name}': {id}");
                }
                else
                {
                    LogDebug($"[DEBUG] GetMetadata: Failed to parse ID from name: '{info.Name}'");
                }
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                LogDebug($"No DLsite ID found for {info.Name} in provider IDs or name");
                return result;
            }

            var cfg = Plugin.PluginConfig;
            var backendUrl = cfg?.BackendUrl?.TrimEnd('/') ?? "http://127.0.0.1:8585";
            var tokenPresent = !string.IsNullOrWhiteSpace(cfg?.ApiToken);
            var enableLogging = cfg?.EnableLogging == true;
            LogInformation($"[Config] Using backendUrl='{backendUrl}', tokenPresent={tokenPresent}, enableLogging={enableLogging}");

            LogInformation($"Fetching metadata for DLsite ID: {id}");

            var requestUrl = $"{backendUrl}/api/dlsite/{id}";

            using var client = CreateClientWithToken();
            string json;
            try
            {
                var response = await client.GetAsync(requestUrl, cancellationToken);
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync(cancellationToken);
                LogDebug($"Received response from backend: {json}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to fetch metadata from backend for id={id} url={requestUrl}", ex);
                return result;
            }

            JsonElement apiResponse;
            try
            {
                apiResponse = JsonDocument.Parse(json).RootElement;
            }
            catch (Exception ex)
            {
                LogError($"Failed to parse JSON metadata for id={id}", ex);
                return result;
            }

            // Check if API response is successful and extract data
            if (!apiResponse.TryGetProperty("success", out var successProp) ||
                !successProp.GetBoolean() ||
                !apiResponse.TryGetProperty("data", out var root))
            {
                LogWarning($"API response indicates failure or missing data for id={id}");
                LogWarning($"Full API response: {json}");
                return result;
            }

            // Set external ID (ensure it's always set correctly)
            result.Item.SetProviderId("DLsite", id);

            // Log the raw data we received
            LogDebug($"Raw API data received for {id}: {root}");

            // Set basic metadata - always set even if empty to ensure overwrite
            var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            result.Item.Name = !string.IsNullOrWhiteSpace(title) ? title : $"DLsite Content {id}";
            LogDebug($"Set Name to: '{result.Item.Name}'");

            if (root.TryGetProperty("originalTitle", out var ot))
            {
                result.Item.OriginalTitle = ot.GetString();
                LogDebug($"Set OriginalTitle to: '{result.Item.OriginalTitle}'");
            }

            if (root.TryGetProperty("description", out var d))
            {
                result.Item.Overview = d.GetString();
                LogDebug($"Set Overview to: '{result.Item.Overview}'");
            }

            if (root.TryGetProperty("year", out var y) && y.TryGetInt32(out var year))
            {
                result.Item.ProductionYear = year;
                LogDebug($"Set ProductionYear to: {result.Item.ProductionYear}");
            }

            if (root.TryGetProperty("rating", out var r) && r.ValueKind == JsonValueKind.Number)
            {
                var rating = r.GetDouble();
                result.Item.CommunityRating = (float)(rating * 2); // Convert to 10-point scale
                LogDebug($"Set CommunityRating to: {result.Item.CommunityRating}");
            }

            if (root.TryGetProperty("releaseDate", out var rd) && rd.ValueKind == JsonValueKind.String &&
                System.DateTimeOffset.TryParse(rd.GetString(), out var releaseDate))
            {
                result.Item.PremiereDate = releaseDate.UtcDateTime;
                LogDebug($"Set PremiereDate to: {result.Item.PremiereDate}");
            }

            // Clear existing people and add new ones
            result.People.Clear();
            if (root.TryGetProperty("people", out var people) && people.ValueKind == JsonValueKind.Array)
            {
                try
                {
                    LogDebug($"Processing people array with {people.GetArrayLength()} items");
                    foreach (var person in people.EnumerateArray())
                    {
                        try
                        {
                            var name = person.TryGetProperty("name", out var n) ? n.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                var type = person.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
                                var role = person.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null;

                                var personInfo = new PersonInfo
                                {
                                    Name = name,
                                    Role = role ?? string.Empty,
                                    Type = type switch
                                    {
                                        "Director" => PersonKind.Director,
                                        "Writer" => PersonKind.Writer,
                                        _ => PersonKind.Actor
                                    }
                                };

                                result.People.Add(personInfo);
                                LogDebug($"Added person: {name} ({type}) - {role}");
                            }
                        }
                        catch (Exception personEx)
                        {
                            LogError($"Error processing individual person: {personEx.Message}", personEx);
                        }
                    }

                    LogDebug($"Added {result.People.Count} people");
                }
                catch (Exception ex)
                {
                    LogError("Error processing people array", ex);
                    result.People.Clear(); // Clear any partially added people
                }
            }
            else
            {
                LogDebug("No people array found or people array is empty");
            }

            // Clear and set genres (always set array, even if empty)
            try
            {
                List<string> genresList = new();
                if (root.TryGetProperty("genres", out var genresProp) && genresProp.ValueKind == JsonValueKind.Array)
                {
                    LogDebug($"Processing genres array with {genresProp.GetArrayLength()} items");
                    foreach (var genre in genresProp.EnumerateArray())
                    {
                        if (genre.ValueKind == JsonValueKind.String)
                        {
                            var genreStr = genre.GetString();
                            if (!string.IsNullOrWhiteSpace(genreStr))
                            {
                                genresList.Add(genreStr);
                            }
                        }
                    }
                }

                result.Item.Genres = genresList.ToArray();
                LogDebug($"Set {result.Item.Genres.Length} genres: {string.Join(", ", result.Item.Genres)}");
            }
            catch (Exception ex)
            {
                LogError("Error processing genres", ex);
                result.Item.Genres = Array.Empty<string>();
            }

            // Clear and set studios (always set array, even if empty)
            try
            {
                List<string> studiosList = new();
                if (root.TryGetProperty("studios", out var studiosProp) && studiosProp.ValueKind == JsonValueKind.Array)
                {
                    LogDebug($"Processing studios array with {studiosProp.GetArrayLength()} items");
                    foreach (var studio in studiosProp.EnumerateArray())
                    {
                        if (studio.ValueKind == JsonValueKind.String)
                        {
                            var studioStr = studio.GetString();
                            if (!string.IsNullOrWhiteSpace(studioStr))
                            {
                                studiosList.Add(studioStr);
                            }
                        }
                    }
                }

                result.Item.Studios = studiosList.ToArray();
                LogDebug($"Set {result.Item.Studios.Length} studios: {string.Join(", ", result.Item.Studios)}");
            }
            catch (Exception ex)
            {
                LogError("Error processing studios", ex);
                result.Item.Studios = Array.Empty<string>();
            }

            // Clear and set series/tags (always set array, even if empty)
            try
            {
                List<string> tagsList = new();
                if (root.TryGetProperty("series", out var seriesProp) && seriesProp.ValueKind == JsonValueKind.Array)
                {
                    LogDebug($"Processing series array with {seriesProp.GetArrayLength()} items");
                    foreach (var series in seriesProp.EnumerateArray())
                    {
                        if (series.ValueKind == JsonValueKind.String)
                        {
                            var seriesStr = series.GetString();
                            if (!string.IsNullOrWhiteSpace(seriesStr))
                            {
                                tagsList.Add(seriesStr);
                            }
                        }
                    }
                }

                result.Item.Tags = tagsList.ToArray();
                LogDebug($"Set {result.Item.Tags.Length} tags: {string.Join(", ", result.Item.Tags)}");
            }
            catch (Exception ex)
            {
                LogError("Error processing series/tags", ex);
                result.Item.Tags = Array.Empty<string>();
            }

            result.HasMetadata = true;
            LogInformation($"Successfully fetched metadata for DLsite ID: {id}, Title: {result.Item.Name}");
            LogInformation($"Final metadata summary: Name='{result.Item.Name}', Overview length={result.Item.Overview?.Length ?? 0}, Genres={result.Item.Genres?.Length ?? 0}, People={result.People.Count}");

            return result;
        }
        catch (Exception ex)
        {
            LogError("Unexpected error in GetMetadata", ex);
            return new MetadataResult<Movie> { Item = new Movie(), HasMetadata = false };
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// Searches for DLsite content based on provided search information.
    /// Supports both ID-based and text-based searches with Japanese keyword support.
    /// </summary>
    /// <param name="searchInfo">Search criteria including name, year, and existing provider IDs.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of search results matching the criteria.</returns>
    /// <example>
    /// Test case - Search by Japanese text:
    /// <code>
    /// var searchInfo = new MovieInfo { Name = "恋爱" };
    /// var results = await provider.GetSearchResults(searchInfo, CancellationToken.None);
    /// Assert.True(results.Any());
    /// Assert.All(results, r => Assert.Contains("恋", r.Name));
    /// </code>
    ///
    /// Test case - Search by existing ID:
    /// <code>
    /// var searchInfo = new MovieInfo
    /// {
    ///     Name = "Test",
    ///     ProviderIds = { ["DLsite"] = "RJ01402281" }
    /// };
    /// var results = await provider.GetSearchResults(searchInfo, CancellationToken.None);
    /// Assert.Single(results);
    /// Assert.Equal("RJ01402281", results.First().ProviderIds["DLsite"]);
    /// </code>
    ///
    /// Test case - Search by DLsite ID in name:
    /// <code>
    /// var searchInfo = new MovieInfo { Name = "RJ01402281" };
    /// var results = await provider.GetSearchResults(searchInfo, CancellationToken.None);
    /// Assert.Single(results);
    /// Assert.Equal("RJ01402281", results.First().ProviderIds["DLsite"]);
    /// </code>
    /// </example>
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        MovieInfo searchInfo,
        CancellationToken cancellationToken)
    {
        LogInformation(
            $"GetSearchResults called with: Name='{searchInfo.Name}', Year={searchInfo.Year}, ProviderIds={string.Join(", ", searchInfo.ProviderIds.Select(p => $"{p.Key}:{p.Value}"))}");

        var results = new List<RemoteSearchResult>();

        // First priority: Check if there's a DLsite ID in ProviderIds (from External ID field)
        var existingId = searchInfo.GetProviderId("DLsite");
        if (!string.IsNullOrWhiteSpace(existingId))
        {
            LogInformation($"Found DLsite ID in ProviderIds (External ID field): {existingId}");
            var detailResult = await GetDetailByIdAsync(existingId, cancellationToken);
            if (detailResult != null)
            {
                results.Add(detailResult);
            }
            else
            {
                LogInformation($"DLsite ID search failed for: {existingId}, no content found");
            }

            return results;
        }

        var query = searchInfo.Name;
        if (string.IsNullOrWhiteSpace(query))
        {
            return results;
        }

        LogInformation($"Processing search request: {query}");

        // Second priority: Try to parse the name field as ID
        if (TryParseDLsiteId(query, out var dlsiteId))
        {
            LogInformation($"Detected DLsite ID in name field: {dlsiteId}");
            var detailResult = await GetDetailByIdAsync(dlsiteId, cancellationToken);
            if (detailResult != null)
            {
                results.Add(detailResult);
            }
            else
            {
                LogInformation($"DLsite ID search failed for: {dlsiteId}, no content found");
            }

            return results;
        }

        // Third priority: Perform title search
        LogInformation($"Performing title search: {query}");
        return await PerformTitleSearchAsync(query, cancellationToken);
    }

    /// <summary>
    /// Attempts to parse a DLsite product ID from various input formats.
    /// Supports both direct IDs (RJ/VJ format) and URLs containing product IDs.
    /// </summary>
    /// <param name="input">The input string to parse.</param>
    /// <param name="id">The extracted DLsite ID if parsing succeeds.</param>
    /// <returns>True if a valid ID was extracted, false otherwise.</returns>
    /// <example>
    /// Test cases:
    /// <code>
    /// // Direct product ID
    /// Assert.True(TryParseDLsiteId("RJ01402281", out var id1));
    /// Assert.Equal("RJ01402281", id1);
    ///
    /// // VJ series ID
    /// Assert.True(TryParseDLsiteId("VJ123456", out var id2));
    /// Assert.Equal("VJ123456", id2);
    ///
    /// // URL with product ID
    /// Assert.True(TryParseDLsiteId("https://www.dlsite.com/maniax/work/=/product_id/RJ01402281.html", out var id3));
    /// Assert.Equal("RJ01402281", id3);
    ///
    /// // Invalid input
    /// Assert.False(TryParseDLsiteId("invalid", out var id4));
    /// Assert.False(TryParseDLsiteId("", out var id5));
    /// </code>
    /// </example>
    private bool TryParseDLsiteId(string input, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = input.Trim().ToUpperInvariant();

        LogDebug($"[DEBUG] TryParseDLsiteId: input='{input}', cleaned='{cleaned}'");

        // Check if it's a DLsite ID format (RJ123456, VJ123456, etc.)
        if (System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^[RV]J\d+$"))
        {
            id = cleaned;
            LogDebug($"[DEBUG] TryParseDLsiteId: Matched direct ID pattern, returning id='{id}'");
            return true;
        }

        // Check if it's a URL containing the ID
        var urlMatch = System.Text.RegularExpressions.Regex.Match(input, @"product_id/([RV]J\d+)");
        if (urlMatch.Success)
        {
            id = urlMatch.Groups[1].Value.ToUpperInvariant();
            LogDebug($"[DEBUG] TryParseDLsiteId: Matched URL pattern, returning id='{id}'");
            return true;
        }

        LogDebug($"[DEBUG] TryParseDLsiteId: No match found for input='{input}'");
        return false;
    }

    /// <summary>
    /// Gets content details by ID and converts them to a search result.
    /// This method handles the backend API communication and error handling.
    /// </summary>
    /// <param name="id">The DLsite product ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A search result if content is found, null otherwise.</returns>
    /// <example>
    /// Test case - Valid ID:
    /// <code>
    /// var result = await GetDetailByIdAsync("RJ01402281", CancellationToken.None);
    /// Assert.NotNull(result);
    /// Assert.Equal("RJ01402281", result.ProviderIds["DLsite"]);
    /// Assert.NotEmpty(result.Name);
    /// </code>
    ///
    /// Test case - Invalid ID:
    /// <code>
    /// var result = await GetDetailByIdAsync("RJ999999999", CancellationToken.None);
    /// Assert.Null(result);
    /// </code>
    /// </example>
    private async Task<RemoteSearchResult?> GetDetailByIdAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var backendUrl = Plugin.PluginConfig?.BackendUrl?.TrimEnd('/') ?? "http://localhost:8585";
            var requestUrl = $"{backendUrl}/api/dlsite/{id}";

            using var client = CreateClientWithToken();
            var response = await client.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            var apiResponse = JsonDocument.Parse(json).RootElement;
            if (!apiResponse.TryGetProperty("success", out var successProp) ||
                !successProp.GetBoolean() ||
                !apiResponse.TryGetProperty("data", out var data))
            {
                LogWarning($"API response indicates failure for ID: {id}");
                return null;
            }

            var title = data.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
            var description = data.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
            var year = data.TryGetProperty("year", out var yearProp) && yearProp.TryGetInt32(out var y) ? y : (int?)null;
            var primary = data.TryGetProperty("primary", out var primaryProp) ? primaryProp.GetString() : null;

            if (!string.IsNullOrWhiteSpace(title))
            {
                LogInformation($"Found content by ID {id}: {title}");
                return new RemoteSearchResult
                {
                    Name = title,
                    Overview = description,
                    ProductionYear = year,
                    ImageUrl = primary,
                    ProviderIds = { ["DLsite"] = id }
                };
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to get detail for DLsite ID: {id}", ex);
        }

        return null;
    }

    /// <summary>
    /// Performs a title-based search using the backend service.
    /// Handles both array and single object responses from the API.
    /// </summary>
    /// <param name="title">The search title or keyword (supports Japanese).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of search results matching the title.</returns>
    /// <example>
    /// Test case - Multiple results:
    /// <code>
    /// var results = await PerformTitleSearchAsync("恋爱", CancellationToken.None);
    /// Assert.True(results.Count() > 1);
    /// Assert.All(results, r => Assert.Contains("恋", r.Name));
    /// </code>
    ///
    /// Test case - No results:
    /// <code>
    /// var results = await PerformTitleSearchAsync("NonexistentTitle123", CancellationToken.None);
    /// Assert.Empty(results);
    /// </code>
    /// </example>
    private async Task<IEnumerable<RemoteSearchResult>> PerformTitleSearchAsync(string title, CancellationToken cancellationToken)
    {
        var results = new List<RemoteSearchResult>();

        var backendUrl = Plugin.PluginConfig?.BackendUrl?.TrimEnd('/') ?? "http://localhost:8585";
        var url = $"{backendUrl}/api/dlsite/search?title={System.Net.WebUtility.UrlEncode(title)}&max=10";
        using var client = CreateClientWithToken();
        string json;
        try
        {
            json = await client.GetStringAsync(url, cancellationToken);
        }
        catch (Exception ex)
        {
            LogError($"Failed to fetch search results from backend url={url}", ex);
            return results;
        }

        JsonElement apiResponse;
        try
        {
            apiResponse = JsonDocument.Parse(json).RootElement;
        }
        catch (Exception ex)
        {
            LogError("Failed to parse search JSON", ex);
            return results;
        }

        // Check if API response is successful and extract data
        if (!apiResponse.TryGetProperty("success", out var successProp) ||
            !successProp.GetBoolean() ||
            !apiResponse.TryGetProperty("data", out var data))
        {
            LogWarning($"Search API response indicates failure or missing data for title={title}");
            return results;
        }

        // Handle both array and single object responses
        if (data.ValueKind == JsonValueKind.Array)
        {
            // Multiple search results
            foreach (var item in data.EnumerateArray())
            {
                ProcessDLsiteSearchResultItem(item, results);
            }
        }
        else if (data.ValueKind == JsonValueKind.Object)
        {
            // Single search result
            ProcessDLsiteSearchResultItem(data, results);
        }

        LogInformation($"Title search returned {results.Count} results for: {title}");
        return results;
    }

    /// <summary>
    /// Processes a single search result item from the backend API response.
    /// Extracts relevant metadata and adds it to the results collection.
    /// </summary>
    /// <param name="item">The JSON element containing search result data.</param>
    /// <param name="results">The collection to add the processed result to.</param>
    /// <example>
    /// Test case - Valid item:
    /// <code>
    /// var jsonItem = JsonDocument.Parse(@"{
    ///     ""id"": ""RJ123456"",
    ///     ""title"": ""Test Title"",
    ///     ""description"": ""Test Description"",
    ///     ""year"": 2024,
    ///     ""primary"": ""https://example.com/image.jpg""
    /// }").RootElement;
    ///
    /// var results = new List&lt;RemoteSearchResult&gt;();
    /// ProcessDLsiteSearchResultItem(jsonItem, results);
    ///
    /// Assert.Single(results);
    /// var result = results.First();
    /// Assert.Equal("Test Title", result.Name);
    /// Assert.Equal("RJ123456", result.ProviderIds["DLsite"]);
    /// Assert.Equal(2024, result.ProductionYear);
    /// </code>
    /// </example>
    private void ProcessDLsiteSearchResultItem(JsonElement item, List<RemoteSearchResult> results)
    {
        var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var name = item.TryGetProperty("title", out var nameProp) ? nameProp.GetString() : null;
        var overview = item.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
        var year = item.TryGetProperty("year", out var yearProp) && yearProp.TryGetInt32(out var y) ? y : (int?)null;
        var primary = item.TryGetProperty("primary", out var p) ? p.GetString() : null;

        if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
        {
            var resultItem = new RemoteSearchResult
            {
                Name = name,
                Overview = overview,
                ProductionYear = year,
                ImageUrl = primary,
                ProviderIds = { ["DLsite"] = id }
            };
            results.Add(resultItem);
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// Gets an HTTP response for the specified image URL.
    /// This method is used by Jellyfin to download images for caching.
    /// </summary>
    /// <param name="url">The image URL to fetch.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>HTTP response containing the image data.</returns>
    /// <example>
    /// Test case - Valid image URL:
    /// <code>
    /// var response = await provider.GetImageResponse("https://example.com/image.jpg", CancellationToken.None);
    /// Assert.True(response.IsSuccessStatusCode);
    /// Assert.True(response.Content.Headers.ContentLength > 0);
    /// </code>
    /// </example>
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var http = new HttpClient();
        return http.GetAsync(url, cancellationToken);
    }

    /// <summary>
    /// Creates an HTTP client with optional authentication token.
    /// The token is added to the request headers if configured in the plugin settings.
    /// </summary>
    /// <returns>A configured HTTP client instance.</returns>
    /// <example>
    /// Test case - Client with token:
    /// <code>
    /// // Assuming Plugin.PluginConfig.ApiToken = "test-token"
    /// var client = CreateClientWithToken();
    /// Assert.True(client.DefaultRequestHeaders.Contains("X-API-Token"));
    /// Assert.Equal("test-token", client.DefaultRequestHeaders.GetValues("X-API-Token").First());
    /// </code>
    ///
    /// Test case - Client without token:
    /// <code>
    /// // Assuming Plugin.PluginConfig.ApiToken = null
    /// var client = CreateClientWithToken();
    /// Assert.False(client.DefaultRequestHeaders.Contains("X-API-Token"));
    /// </code>
    /// </example>
    private HttpClient CreateClientWithToken()
    {
        var client = new HttpClient();
        var token = Plugin.PluginConfig?.ApiToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Add("X-API-Token", token);
        }

        return client;
    }
}
