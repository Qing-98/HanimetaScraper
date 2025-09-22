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
/// </summary>
public class DLsiteMetadataProvider
    : IRemoteMetadataProvider<Movie, MovieInfo>, IRemoteSearchProvider<MovieInfo>, IHasOrder
{
    private readonly ILogger<DLsiteMetadataProvider> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DLsiteMetadataProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DLsiteMetadataProvider(ILogger<DLsiteMetadataProvider> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public string Name => "DLsite";

    /// <inheritdoc />
    public int Order => 0;

    private void LogError(string message, Exception? ex = null)
    {
        try
        {
            logger.LogError(ex, "{Message}", message);
        }
        catch
        {
        }
    }

    /// <inheritdoc />
    public async Task<MetadataResult<Movie>> GetMetadata(
        MovieInfo info,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "GetMetadata called with MovieInfo: Name={Name}, ProviderIds={ProviderIds}",
            info.Name,
            string.Join(", ", info.ProviderIds.Select(p => $"{p.Key}:{p.Value}")));

        var result = new MetadataResult<Movie> { Item = new Movie(), HasMetadata = false };

        var id = info.GetProviderId("DLsite");
        if (string.IsNullOrWhiteSpace(id))
        {
            logger.LogDebug("No DLsite ID found for {Name}", info.Name);
            return result;
        }

        logger.LogInformation("Fetching metadata for DLsite ID: {Id}", id);

        var backendUrl = Plugin.PluginConfig.BackendUrl?.TrimEnd('/') ?? "http://localhost:8585";
        var requestUrl = $"{backendUrl}/api/dlsite/{id}";

        using var client = CreateClientWithToken();
        string json;
        try
        {
            var response = await client.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            json = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogDebug("Received response from backend: {Response}", json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch metadata from backend for id={Id} url={RequestUrl}", id, requestUrl);
            return result;
        }

        JsonElement apiResponse;
        try
        {
            apiResponse = JsonDocument.Parse(json).RootElement;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse JSON metadata for id={Id}", id);
            return result;
        }

        // Check if API response is successful and extract data
        if (!apiResponse.TryGetProperty("success", out var successProp) ||
            !successProp.GetBoolean() ||
            !apiResponse.TryGetProperty("data", out var root))
        {
            logger.LogWarning("API response indicates failure or missing data for id={Id}", id);
            return result;
        }

        // Set external ID
        result.Item.SetProviderId("DLsite", id);

        // Set basic metadata (注意：使用小写属性名)
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
            foreach (var person in people.EnumerateArray())
            {
                var name = person.TryGetProperty("name", out var n) ? n.GetString() : null;
                var type = person.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
                var role = person.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    result.People.Add(new PersonInfo
                    {
                        Name = name,
                        Role = role,
                        Type = type switch
                        {
                            "Director" => PersonKind.Director,
                            "Writer" => PersonKind.Writer,
                            _ => PersonKind.Actor
                        }
                    });
                }
            }
        }

        // Map Genres / Studios / Series if present in backend JSON (使用小写属性名)
        try
        {
            if (root.TryGetProperty("genres", out var genresProp) && genresProp.ValueKind == JsonValueKind.Array)
            {
                var genres = genresProp.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();

                if (genres.Length > 0)
                {
                    // 设置Genres数组
                    result.Item.Genres = genres;
                    logger.LogDebug("Added {Count} genres: {Genres}", genres.Length, string.Join(", ", genres));
                }
            }

            if (root.TryGetProperty("studios", out var studiosProp) && studiosProp.ValueKind == JsonValueKind.Array)
            {
                var studios = studiosProp.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();

                if (studios.Length > 0)
                {
                    // 设置Studios数组
                    result.Item.Studios = studios;
                    logger.LogDebug("Added {Count} studios: {Studios}", studios.Length, string.Join(", ", studios));
                }
            }

            if (root.TryGetProperty("series", out var seriesProp) && seriesProp.ValueKind == JsonValueKind.Array)
            {
                var series = seriesProp.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();

                if (series.Length > 0)
                {
                    // 对于Series，设置到Tags数组
                    result.Item.Tags = series;
                    logger.LogDebug("Added {Count} series as tags: {Series}", series.Length, string.Join(", ", series));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to map list properties from backend JSON");
        }

        result.HasMetadata = true;
        logger.LogInformation("Successfully fetched metadata for DLsite ID: {Id}, Title: {Title}", id, result.Item.Name);
        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        MovieInfo searchInfo,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "GetSearchResults called with: Name='{Name}', Year={Year}, ProviderIds={ProviderIds}",
            searchInfo.Name,
            searchInfo.Year,
            string.Join(", ", searchInfo.ProviderIds.Select(p => $"{p.Key}:{p.Value}")));

        var results = new List<RemoteSearchResult>();

        // 首先检查是否已经有DLsite ID在ProviderIds中
        var existingId = searchInfo.GetProviderId("DLsite");
        if (!string.IsNullOrWhiteSpace(existingId))
        {
            logger.LogInformation("Found existing DLsite ID in ProviderIds: {Id}", existingId);
            var detailResult = await GetDetailByIdAsync(existingId, cancellationToken);
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

        logger.LogInformation("Processing search request: {Query}", query);

        // 检查是否输入的是DLsite ID
        if (TryParseDLsiteId(query, out var dlsiteId))
        {
            logger.LogInformation("Detected DLsite ID search: {Id}", dlsiteId);
            var detailResult = await GetDetailByIdAsync(dlsiteId, cancellationToken);
            if (detailResult != null)
            {
                results.Add(detailResult);
            }

            return results;
        }

        // 如果不是ID，则进行标题搜索
        logger.LogInformation("Performing title search: {Title}", query);
        return await PerformTitleSearchAsync(query, cancellationToken);
    }

    /// <summary>
    /// 尝试解析DLsite ID.
    /// </summary>
    private bool TryParseDLsiteId(string input, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = input.Trim().ToUpperInvariant();

        // 检查是否为DLsite ID格式 (RJ123456, VJ123456等)
        if (System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^[RV]J\d+$"))
        {
            id = cleaned;
            return true;
        }

        // 检查是否为包含ID的URL
        var urlMatch = System.Text.RegularExpressions.Regex.Match(input, @"product_id/([RV]J\d+)");
        if (urlMatch.Success)
        {
            id = urlMatch.Groups[1].Value.ToUpperInvariant();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 通过ID获取详情并转换为搜索结果.
    /// </summary>
    private async Task<RemoteSearchResult?> GetDetailByIdAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var backendUrl = Plugin.PluginConfig.BackendUrl?.TrimEnd('/') ?? "http://localhost:8585";
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
                logger.LogWarning("API response indicates failure for ID: {Id}", id);
                return null;
            }

            var title = data.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
            var description = data.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
            var year = data.TryGetProperty("year", out var yearProp) && yearProp.TryGetInt32(out var y) ? y : (int?)null;
            var primary = data.TryGetProperty("primary", out var primaryProp) ? primaryProp.GetString() : null;

            if (!string.IsNullOrWhiteSpace(title))
            {
                logger.LogInformation("Found content by ID {Id}: {Title}", id, title);
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
            logger.LogError(ex, "Failed to get detail for DLsite ID: {Id}", id);
        }

        return null;
    }

    /// <summary>
    /// 执行标题搜索.
    /// </summary>
    private async Task<IEnumerable<RemoteSearchResult>> PerformTitleSearchAsync(string title, CancellationToken cancellationToken)
    {
        var results = new List<RemoteSearchResult>();

        var backendUrl = Plugin.PluginConfig.BackendUrl?.TrimEnd('/') ?? "http://localhost:8585";
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
            logger.LogWarning("Search API response indicates failure or missing data for title={Title}", title);
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

        logger.LogInformation("Title search returned {Count} results for: {Title}", results.Count, title);
        return results;
    }

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
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var http = new HttpClient();
        return http.GetAsync(url, cancellationToken);
    }

    private HttpClient CreateClientWithToken()
    {
        var client = new HttpClient();
        var token = Plugin.PluginConfig.ApiToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Add("X-API-Token", token);
        }

        return client;
    }
}
