using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.HanimeScraper;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HanimeScraper.Providers;

/// <summary>
/// Hanime metadata provider that interacts with backend service without page parsing.
/// This provider handles metadata extraction and search functionality for Hanime content
/// by communicating with the scraper backend service via HTTP API calls.
/// </summary>
/// <remarks>
/// This provider supports:
/// - Metadata extraction by Hanime ID
/// - Search functionality with text queries
/// - External ID management
/// - Image URL extraction
/// - Personnel information mapping.
/// </remarks>
/// <example>
/// Basic usage in Jellyfin:
/// <code>
/// // The provider is automatically registered via dependency injection
/// // Search for content: "Love Story"
/// // Get metadata by ID: "86994"
/// </code>
/// </example>
public class HanimeMetadataProvider
    : IRemoteMetadataProvider<Movie, MovieInfo>, IRemoteSearchProvider<MovieInfo>, IHasOrder
{
    private readonly ILogger<HanimeMetadataProvider> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HanimeMetadataProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for tracking operations.</param>
    public HanimeMetadataProvider(ILogger<HanimeMetadataProvider> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    /// <summary>
    /// Gets the name of this metadata provider.
    /// </summary>
    public string Name => "Hanime";

    /// <inheritdoc />
    /// <summary>
    /// Gets the execution order for this provider (0 = highest priority).
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
    /// Gets metadata for a movie item using the Hanime provider ID.
    /// This method fetches detailed metadata from the backend service and maps it to Jellyfin's movie entity.
    /// </summary>
    /// <param name="info">Movie information containing the Hanime provider ID and other metadata.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A metadata result containing the populated movie entity.</returns>
    /// <example>
    /// Test case - Valid Hanime ID:
    /// <code>
    /// var movieInfo = new MovieInfo
    /// {
    ///     Name = "Test Movie",
    ///     ProviderIds = { ["Hanime"] = "86994" }
    /// };
    /// var result = await provider.GetMetadata(movieInfo, CancellationToken.None);
    /// Assert.True(result.HasMetadata);
    /// Assert.NotNull(result.Item);
    /// Assert.Equal("86994", result.Item.GetProviderId("Hanime"));
    /// </code>
    ///
    /// Test case - Missing Hanime ID:
    /// <code>
    /// var movieInfo = new MovieInfo { Name = "Test Movie" };
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

            // Try to get Hanime ID from provider IDs
            var id = info.GetProviderId("Hanime");

            // If no ID found in provider IDs, try to parse from the name
            if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(info.Name))
            {
                if (TryParseHanimeId(info.Name, out var parsedId))
                {
                    id = parsedId;
                    LogInformation($"Parsed Hanime ID from name '{info.Name}': {id}");
                }
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                LogDebug($"No Hanime ID found for {info.Name} in provider IDs or name");
                return result;
            }

            var cfg = Plugin.PluginConfig;
            var backendUrl = cfg?.BackendUrl?.TrimEnd('/') ?? "http://127.0.0.1:8585";
            var tokenPresent = !string.IsNullOrWhiteSpace(cfg?.ApiToken);
            var enableLogging = cfg?.EnableLogging == true;
            LogInformation($"[Config] Using backendUrl='{backendUrl}', tokenPresent={tokenPresent}, enableLogging={enableLogging}");

            LogInformation($"Fetching metadata for Hanime ID: {id}");

            var requestUrl = $"{backendUrl}/api/hanime/{id}";

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
                return result;
            }

            // Set external ID (ensure it's always set correctly)
            result.Item.SetProviderId("Hanime", id);

            // Set basic metadata (using lowercase property names as per API spec)
            result.Item.Name = root.TryGetProperty("title", out var t) ? (t.GetString() ?? info.Name ?? string.Empty) : (info.Name ?? string.Empty);
            if (root.TryGetProperty("originalTitle", out var ot) && !string.IsNullOrWhiteSpace(ot.GetString()))
            {
                result.Item.OriginalTitle = ot.GetString();
            }

            if (root.TryGetProperty("description", out var d) && !string.IsNullOrWhiteSpace(d.GetString()))
            {
                result.Item.Overview = d.GetString();
            }

            if (root.TryGetProperty("year", out var y) && y.TryGetInt32(out var year))
            {
                result.Item.ProductionYear = year;
            }

            if (root.TryGetProperty("rating", out var r) && r.ValueKind == JsonValueKind.Number)
            {
                result.Item.CommunityRating = (float)r.GetDouble() * 2;
            }

            if (root.TryGetProperty("releaseDate", out var rd) && rd.ValueKind == JsonValueKind.String &&
                System.DateTimeOffset.TryParse(rd.GetString(), out var releaseDate))
            {
                result.Item.PremiereDate = releaseDate.UtcDateTime;
            }

            // People information
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
                            var type = person.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
                            var role = person.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null;

                            if (!string.IsNullOrWhiteSpace(name))
                            {
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

            // Map Genres / Studios / Series if present in backend JSON (using lowercase property names)
            try
            {
                // Process genres with safe handling
                if (root.TryGetProperty("genres", out var genresProp) && genresProp.ValueKind == JsonValueKind.Array)
                {
                    var genres = new List<string>();
                    foreach (var genreElement in genresProp.EnumerateArray())
                    {
                        if (genreElement.ValueKind == JsonValueKind.String)
                        {
                            var genreStr = genreElement.GetString();
                            if (!string.IsNullOrWhiteSpace(genreStr))
                            {
                                genres.Add(genreStr);
                            }
                        }
                    }

                    if (genres.Any())
                    {
                        result.Item.Genres = genres.ToArray();
                        LogDebug($"Added {genres.Count} genres: {string.Join(", ", genres)}");
                    }
                    else
                    {
                        result.Item.Genres = Array.Empty<string>();
                        LogDebug("No valid genres found, set empty array");
                    }
                }
                else
                {
                    result.Item.Genres = Array.Empty<string>();
                    LogDebug("No genres property found, set empty array");
                }

                // Process studios with safe handling
                if (root.TryGetProperty("studios", out var studiosProp) && studiosProp.ValueKind == JsonValueKind.Array)
                {
                    var studios = new List<string>();
                    foreach (var studioElement in studiosProp.EnumerateArray())
                    {
                        if (studioElement.ValueKind == JsonValueKind.String)
                        {
                            var studioStr = studioElement.GetString();
                            if (!string.IsNullOrWhiteSpace(studioStr))
                            {
                                studios.Add(studioStr);
                            }
                        }
                    }

                    if (studios.Any())
                    {
                        result.Item.Studios = studios.ToArray();
                        LogDebug($"Added {studios.Count} studios: {string.Join(", ", studios)}");
                    }
                    else
                    {
                        result.Item.Studios = Array.Empty<string>();
                        LogDebug("No valid studios found, set empty array");
                    }
                }
                else
                {
                    result.Item.Studios = Array.Empty<string>();
                    LogDebug("No studios property found, set empty array");
                }

                // Process series with safe handling
                if (root.TryGetProperty("series", out var seriesProp) && seriesProp.ValueKind == JsonValueKind.Array)
                {
                    var series = new List<string>();
                    foreach (var seriesElement in seriesProp.EnumerateArray())
                    {
                        if (seriesElement.ValueKind == JsonValueKind.String)
                        {
                            var seriesStr = seriesElement.GetString();
                            if (!string.IsNullOrWhiteSpace(seriesStr))
                            {
                                series.Add(seriesStr);
                            }
                        }
                    }

                    if (series.Any())
                    {
                        result.Item.Tags = series.ToArray();
                        LogDebug($"Added {series.Count} series as tags: {string.Join(", ", series)}");
                    }
                    else
                    {
                        result.Item.Tags = Array.Empty<string>();
                        LogDebug("No valid series found, set empty array");
                    }
                }
                else
                {
                    result.Item.Tags = Array.Empty<string>();
                    LogDebug("No series property found, set empty array");
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to map list properties from backend JSON", ex);

                // Ensure all array properties are set to empty arrays to prevent null reference issues
                result.Item.Genres = Array.Empty<string>();
                result.Item.Studios = Array.Empty<string>();
                result.Item.Tags = Array.Empty<string>();
            }

            result.HasMetadata = true;
            LogInformation($"Successfully fetched metadata for Hanime ID: {id}, Title: {result.Item.Name}");
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
    /// Searches for Hanime content based on provided search information.
    /// Supports both ID-based and text-based searches.
    /// </summary>
    /// <param name="searchInfo">Search criteria including name, year, and existing provider IDs.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of search results matching the criteria.</returns>
    /// <example>
    /// Test case - Search by text:
    /// <code>
    /// var searchInfo = new MovieInfo { Name = "Love Story" };
    /// var results = await provider.GetSearchResults(searchInfo, CancellationToken.None);
    /// Assert.True(results.Any());
    /// Assert.All(results, r => Assert.Contains("Love", r.Name, StringComparison.OrdinalIgnoreCase));
    /// </code>
    ///
    /// Test case - Search by existing ID:
    /// <code>
    /// var searchInfo = new MovieInfo
    /// {
    ///     Name = "Test",
    ///     ProviderIds = { ["Hanime"] = "86994" }
    /// };
    /// var results = await provider.GetSearchResults(searchInfo, CancellationToken.None);
    /// Assert.Single(results);
    /// Assert.Equal("86994", results.First().ProviderIds["Hanime"]);
    /// </code>
    ///
    /// Test case - Search by Hanime ID in name:
    /// <code>
    /// var searchInfo = new MovieInfo { Name = "86994" };
    /// var results = await provider.GetSearchResults(searchInfo, CancellationToken.None);
    /// Assert.Single(results);
    /// Assert.Equal("86994", results.First().ProviderIds["Hanime"]);
    /// </code>
    /// </example>
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        MovieInfo searchInfo,
        CancellationToken cancellationToken)
    {
        LogInformation(
            $"GetSearchResults called with: Name='{searchInfo.Name}', Year={searchInfo.Year}, ProviderIds={string.Join(", ", searchInfo.ProviderIds.Select(p => $"{p.Key}:{p.Value}"))}");

        var results = new List<RemoteSearchResult>();

        // First priority: Check if there's a Hanime ID in ProviderIds (from External ID field)
        var existingId = searchInfo.GetProviderId("Hanime");
        if (!string.IsNullOrWhiteSpace(existingId))
        {
            LogInformation($"Found Hanime ID in ProviderIds (External ID field): {existingId}");
            var detailResult = await GetDetailByIdAsync(existingId, cancellationToken);
            if (detailResult != null)
            {
                results.Add(detailResult);
            }
            else
            {
                LogInformation($"Hanime ID search failed for: {existingId}, no content found");
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
        if (TryParseHanimeId(query, out var hanimeId))
        {
            LogInformation($"Detected Hanime ID in name field: {hanimeId}");
            var detailResult = await GetDetailByIdAsync(hanimeId, cancellationToken);
            if (detailResult != null)
            {
                results.Add(detailResult);
            }
            else
            {
                LogInformation($"Hanime ID search failed for: {hanimeId}, no content found");
            }

            return results;
        }

        // Third priority: Perform title search
        LogInformation($"Performing title search: {query}");
        return await PerformTitleSearchAsync(query, cancellationToken);
    }

    /// <summary>
    /// Attempts to parse a Hanime ID from various input formats.
    /// Supports both direct numeric IDs and URLs containing video IDs.
    /// </summary>
    /// <param name="input">The input string to parse.</param>
    /// <param name="id">The extracted Hanime ID if parsing succeeds.</param>
    /// <returns>True if a valid ID was extracted, false otherwise.</returns>
    /// <example>
    /// Test cases:
    /// <code>
    /// // Direct numeric ID
    /// Assert.True(TryParseHanimeId("86994", out var id1));
    /// Assert.Equal("86994", id1);
    ///
    /// // URL with video parameter
    /// Assert.True(TryParseHanimeId("https://hanime1.me/watch?v=12345", out var id2));
    /// Assert.Equal("12345", id2);
    ///
    /// // Invalid input
    /// Assert.False(TryParseHanimeId("invalid", out var id3));
    /// Assert.False(TryParseHanimeId("", out var id4));
    /// </code>
    /// </example>
    private bool TryParseHanimeId(string input, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = input.Trim();

        // Check if it's a pure numeric ID
        if (System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^\d+$"))
        {
            id = cleaned;
            return true;
        }

        // Check if it's a URL containing the ID
        var urlMatch = System.Text.RegularExpressions.Regex.Match(cleaned, @"[?&]v=(\d+)");
        if (urlMatch.Success)
        {
            id = urlMatch.Groups[1].Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets content details by ID and converts them to a search result.
    /// This method handles the backend API communication and error handling.
    /// </summary>
    /// <param name="id">The Hanime content ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A search result if content is found, null otherwise.</returns>
    /// <example>
    /// Test case - Valid ID:
    /// <code>
    /// var result = await GetDetailByIdAsync("86994", CancellationToken.None);
    /// Assert.NotNull(result);
    /// Assert.Equal("86994", result.ProviderIds["Hanime"]);
    /// Assert.NotEmpty(result.Name);
    /// </code>
    ///
    /// Test case - Invalid ID:
    /// <code>
    /// var result = await GetDetailByIdAsync("999999", CancellationToken.None);
    /// Assert.Null(result);
    /// </code>
    /// </example>
    private async Task<RemoteSearchResult?> GetDetailByIdAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var backendUrl = Plugin.PluginConfig?.BackendUrl?.TrimEnd('/') ?? "http://localhost:8585";
            var requestUrl = $"{backendUrl}/api/hanime/{id}";

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
                    ProviderIds = { ["Hanime"] = id }
                };
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to get detail for Hanime ID: {id}", ex);
        }

        return null;
    }

    /// <summary>
    /// Performs a title-based search using the backend service.
    /// Handles both array and single object responses from the API.
    /// </summary>
    /// <param name="title">The search title or keyword.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of search results matching the title.</returns>
    /// <example>
    /// Test case - Multiple results:
    /// <code>
    /// var results = await PerformTitleSearchAsync("Love", CancellationToken.None);
    /// Assert.True(results.Count() > 1);
    /// Assert.All(results, r => Assert.Contains("Love", r.Name, StringComparison.OrdinalIgnoreCase));
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
        var url = $"{backendUrl}/api/hanime/search?title={System.Net.WebUtility.UrlEncode(title)}&max=10";
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
                ProcessHanimeSearchResultItem(item, results);
            }
        }
        else if (data.ValueKind == JsonValueKind.Object)
        {
            // Single search result
            ProcessHanimeSearchResultItem(data, results);
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
    ///     ""id"": ""12345"",
    ///     ""title"": ""Test Title"",
    ///     ""description"": ""Test Description"",
    ///     ""year"": 2024,
    ///     ""primary"": ""https://example.com/image.jpg""
    /// }").RootElement;
    ///
    /// var results = new List&lt;RemoteSearchResult&gt;();
    /// ProcessHanimeSearchResultItem(jsonItem, results);
    ///
    /// Assert.Single(results);
    /// var result = results.First();
    /// Assert.Equal("Test Title", result.Name);
    /// Assert.Equal("12345", result.ProviderIds["Hanime"]);
    /// Assert.Equal(2024, result.ProductionYear);
    /// </code>
    /// </example>
    private void ProcessHanimeSearchResultItem(JsonElement item, List<RemoteSearchResult> results)
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
                ProviderIds = { ["Hanime"] = id }
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
