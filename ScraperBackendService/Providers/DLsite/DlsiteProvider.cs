using System.Globalization;
using System.Text.Json;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ScraperBackendService.Core.Abstractions;
using ScraperBackendService.Core.Normalize;
using ScraperBackendService.Core.Parsing;
using ScraperBackendService.Core.Routing;
using ScraperBackendService.Core.Util;
using ScraperBackendService.Models;
using System.Linq;
using NetUrlHelper = ScraperBackendService.Core.Net.UrlHelper;

namespace ScraperBackendService.Providers.DLsite;

/// <summary>
/// DLsite content provider for scraping doujin and commercial adult content metadata.
/// Supports both Maniax and Pro sites with unified search and detail extraction.
/// </summary>
/// <example>
/// Usage example:
/// var provider = new DlsiteProvider(networkClient, logger);
/// var searchResults = await provider.SearchAsync("恋爱", 10, cancellationToken);
/// var details = await provider.FetchDetailAsync("https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html", cancellationToken);
/// </example>
public sealed class DlsiteProvider : IMediaProvider
{
    private readonly INetworkClient _net;
    private readonly ILogger<DlsiteProvider> _log;

    public DlsiteProvider(INetworkClient net, ILogger<DlsiteProvider> log)
    {
        _net = net;
        _log = log;
    }

    public string Name => "DLsite";

    /// <summary>
    /// Attempts to parse a DLsite product ID from the given input string.
    /// Supports both RJ and VJ prefixed IDs.
    /// </summary>
    /// <param name="input">Input string that may contain a DLsite ID</param>
    /// <param name="id">Extracted DLsite ID if successful</param>
    /// <returns>True if ID was successfully parsed, false otherwise</returns>
    /// <example>
    /// // Parse from URL
    /// if (provider.TryParseId("https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html", out var id))
    /// {
    ///     Console.WriteLine($"Extracted ID: {id}"); // Output: "RJ123456"
    /// }
    ///
    /// // Parse from direct ID
    /// if (provider.TryParseId("RJ01402281", out var id2))
    /// {
    ///     Console.WriteLine($"Direct ID: {id2}"); // Output: "RJ01402281"
    /// }
    /// </example>
    public bool TryParseId(string input, out string id) => IdParsers.TryParseDlsiteId(input, out id);

    /// <summary>
    /// Builds a detail page URL from a DLsite product ID.
    /// Prefers Maniax site for broader content access.
    /// </summary>
    /// <param name="id">DLsite product ID (e.g., "RJ123456")</param>
    /// <returns>Complete URL to the detail page</returns>
    /// <example>
    /// var detailUrl = provider.BuildDetailUrlById("RJ123456");
    /// // Returns: "https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html"
    /// </example>
    public string BuildDetailUrlById(string id)
        => IdParsers.BuildDlsiteDetailUrl(id, preferManiax: true);

    /// <summary>
    /// Unified search URL template for DLsite content.
    /// Targets movie/video content specifically.
    /// </summary>
    private const string UnifiedSearchUrl =
        "https://www.dlsite.com/maniax/fsr/=/keyword/{0}/work_type_category[0]/movie/";

    /// <summary>
    /// Searches for DLsite content using the provided keyword.
    /// Returns a list of search hits with basic product information.
    /// </summary>
    /// <param name="keyword">Search keyword or phrase (supports Japanese text)</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of search hits containing URLs, titles, and cover images</returns>
    /// <example>
    /// // Search with Japanese keyword
    /// var results = await provider.SearchAsync("恋爱", 5, CancellationToken.None);
    /// foreach (var hit in results)
    /// {
    ///     Console.WriteLine($"Title: {hit.Title}");
    ///     Console.WriteLine($"URL: {hit.DetailUrl}");
    ///     Console.WriteLine($"Cover: {hit.CoverUrl}");
    /// }
    ///
    /// // Search with product ID
    /// var results2 = await provider.SearchAsync("RJ123456", 1, CancellationToken.None);
    /// </example>
    public async Task<IReadOnlyList<ScraperBackendService.Core.Abstractions.SearchHit>> SearchAsync(string keyword, int maxResults, CancellationToken ct)
    {
        var normalized = TextNormalizer.NormalizeKeyword(keyword);
        var searchUrl = string.Format(CultureInfo.InvariantCulture, UnifiedSearchUrl, Uri.EscapeDataString(normalized));
        _log.LogInformation("[DLsite] Search: {Url}", searchUrl);

        var html = await _net.GetHtmlAsync(searchUrl, ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var hits = new List<ScraperBackendService.Core.Abstractions.SearchHit>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract search results from the product listing
        foreach (var a in ScrapingUtils.SelectNodes(doc, "//div[@id='search_result_list']//li//a[contains(@href,'/work/=/product_id/')]")
                 ?? Enumerable.Empty<HtmlNode>())
        {
            var href = a.GetAttributeValue("href", "");
            var abs = ScrapingUtils.AbsUrl(href, searchUrl);
            if (!Uri.TryCreate(abs, UriKind.Absolute, out _)) continue;

            var id = ScrapingUtils.ParseDlsiteIdFromUrl(abs);
            if (string.IsNullOrEmpty(id)) continue;

            var detailUrl = IdParsers.BuildDlsiteDetailUrl(id, preferManiax: true);
            if (!seen.Add(detailUrl)) continue;

            string title = a.GetAttributeValue("title", "") ?? "";
            string? cover = a.SelectSingleNode(".//img[@src]")?.GetAttributeValue("src", "");

            hits.Add(new SearchHit(detailUrl, ScrapingUtils.Clean(title), cover ?? ""));
            if (maxResults > 0 && hits.Count >= maxResults) break;
        }

        // Log search results
        if (hits.Count > 0)
        {
            _log.LogInformation("[DLsite] Search completed: {Keyword}, found {Count} hits", keyword, hits.Count);
        }
        else
        {
            _log.LogInformation("[DLsite] Search completed: {Keyword}, no search hits found", keyword);
        }

        return hits;
    }

    /// <summary>
    /// DLsite work detail URL templates for different site sections.
    /// </summary>
    private const string ManiaxWorkUrl = "https://www.dlsite.com/maniax/work/=/product_id/{0}.html";
    private const string ProWorkUrl = "https://www.dlsite.com/pro/work/=/product_id/{0}.html";

    /// <summary>
    /// Fetches detailed metadata for a specific DLsite product.
    /// Attempts multiple site sections (Maniax, Pro) to find the content.
    /// </summary>
    /// <param name="detailUrl">URL of the detail page to scrape</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Complete metadata object or null if extraction fails</returns>
    /// <example>
    /// var metadata = await provider.FetchDetailAsync("https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html", CancellationToken.None);
    /// if (metadata != null)
    /// {
    ///     Console.WriteLine($"Title: {metadata.Title}");
    ///     Console.WriteLine($"Description: {metadata.Description}");
    ///     Console.WriteLine($"Rating: {metadata.Rating}/5");
    ///     Console.WriteLine($"Studio: {string.Join(", ", metadata.Studios)}");
    ///     Console.WriteLine($"Genres: {string.Join(", ", metadata.Genres)}");
    /// }
    /// </example>
    public async Task<HanimeMetadata?> FetchDetailAsync(string detailUrl, CancellationToken ct)
    {
        var id = IdParsers.ParseIdFromDlsiteUrl(detailUrl);
        string[] candidates = string.IsNullOrEmpty(id)
            ? new[] { detailUrl }
            : new[]
              {
                  string.Format(CultureInfo.InvariantCulture, ManiaxWorkUrl, id),
                  string.Format(CultureInfo.InvariantCulture, ProWorkUrl, id)
              };

        Exception? last = null;
        foreach (var url in candidates)
        {
            try
            {
                var meta = await ParseDetailPageAsync(url, ct);
                if (meta != null) return meta;
            }
            catch (Exception ex)
            {
                last = ex;
                _log.LogDebug(ex, "[DLsite] Detail parsing failed: {Url}", url);
            }
        }
        if (last != null) _log.LogWarning(last, "[DLsite] All detail parsing attempts failed.");
        return null;
    }

    /// <summary>
    /// Parses a single DLsite detail page to extract comprehensive metadata.
    /// Handles product information, descriptions, genres, personnel, images, and ratings.
    /// </summary>
    /// <param name="detailUrl">URL of the detail page to parse</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Complete metadata object or null if parsing fails</returns>
    /// <example>
    /// var metadata = await ParseDetailPageAsync("https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html", CancellationToken.None);
    /// Console.WriteLine($"Extracted metadata for {metadata?.ID}");
    /// Console.WriteLine($"Found {metadata?.Genres.Count} genres");
    /// Console.WriteLine($"Found {metadata?.People.Count} personnel entries");
    /// </example>
    private async Task<HanimeMetadata?> ParseDetailPageAsync(string detailUrl, CancellationToken ct)
    {
        var id = IdParsers.ParseIdFromDlsiteUrl(detailUrl);
        if (string.IsNullOrEmpty(id)) return null;

        var html = await _net.GetHtmlAsync(detailUrl, ct);
        if (string.IsNullOrWhiteSpace(html)) return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // First, check if this is a valid DLsite product page
        // Look for key elements that indicate a valid product page
        var titleNode = doc.DocumentNode.SelectSingleNode("//h1[@id='work_name']");
        var workOutlineTable = doc.DocumentNode.SelectSingleNode("//table[@id='work_outline']");
        var workMakerTable = doc.DocumentNode.SelectSingleNode("//table[@id='work_maker']");

        // If we don't find any key content indicators, this is likely a 404 or invalid page
        if (titleNode == null && workOutlineTable == null && workMakerTable == null)
        {
            _log.LogWarning("No valid DLsite product found at URL: {Url}", detailUrl);
            return null;
        }

        var meta = new HanimeMetadata
        {
            ID = id,
            SourceUrls = new List<string> { detailUrl }
        };

        // Extract title from main product name element
        meta.Title = TextNormalizer.Clean(HtmlEx.SelectText(doc, "//h1[@id='work_name']"));

        // Extract description with rich text support
        var descRoot = HtmlEx.SelectSingle(doc, "//div[@itemprop='description' and contains(@class,'work_parts_container')]");
        meta.Description = RichTextExtractor.ExtractFrom(descRoot, new RichTextOptions
        {
            MaxChars = 1600,
            MaxParagraphs = 10
        });

        // Extract studio/circle information
        var maker = HtmlEx.ExtractOutlineCell(doc,
            "//table[@id='work_maker']//tr[.//th[contains(normalize-space(.),'ブランド名') or contains(normalize-space(.),'サークル名')]]//td",
            TextNormalizer.Clean);
        if (!string.IsNullOrWhiteSpace(maker)) meta.Studios.Add(maker);

        // Extract series information
        var series = HtmlEx.ExtractOutlineCell(doc,
            "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'シリーズ')]]//td",
            TextNormalizer.Clean);
        if (!string.IsNullOrWhiteSpace(series)) meta.Series.Add(series);

        // Extract genres/tags
        foreach (var a in HtmlEx.SelectNodes(doc,
                     "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'ジャンル')]]//td//div[contains(@class,'main_genre')]//a")
                 ?? Enumerable.Empty<HtmlNode>())
        {
            var g = TextNormalizer.Clean(a.InnerText);
            if (!string.IsNullOrEmpty(g) && !meta.Genres.Contains(g))
                meta.Genres.Add(g);
        }

        // Extract release date (Japanese format: yyyy年M月d日)
        var rawDate = HtmlEx.ExtractOutlineCellPreferA(doc,
            "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'販売日')]]//td",
            TextNormalizer.Clean);
        var jp = DateTimeNormalizer.ParseJapaneseYmd(rawDate);
        if (jp is { } dt) { meta.ReleaseDate = dt; meta.Year = dt.Year; }

        // Extract personnel information (voice actors, directors, etc.)
        PeopleEx.ExtractDLsitePersonnel(doc, meta);

        // Extract primary image from product slider
        var bigImgNode = HtmlEx.SelectSingle(doc,
            "//*[@id='work_left']//div[contains(@class,'work_slider_container')]//li[contains(@class,'slider_item') and contains(@class,'active')]//img");
        var bigPick = ImageUrlNormalizer.PickJpg(HtmlEx.GetAttr(bigImgNode, "src"), HtmlEx.GetAttr(bigImgNode, "srcset"), detailUrl);
        if (!string.IsNullOrEmpty(bigPick))
        {
            meta.Primary = bigPick;
            meta.Backdrop = bigPick;
        }

        // Extract first thumbnail as backdrop
        var firstThumbNode = HtmlEx.SelectSingle(doc,
            "//*[@id='work_left']//div[contains(@class,'product-slider-data')]/div[1]");
        var firstThumbUrl = NetUrlHelper.Abs(HtmlEx.GetAttr(firstThumbNode, "data-thumb"), detailUrl);
        if (!string.IsNullOrEmpty(firstThumbUrl))
        {
            meta.Backdrop = firstThumbUrl;
            ImageUrlNormalizer.AddThumb(meta, firstThumbUrl);
        }

        // Extract all thumbnail images
        foreach (var node in HtmlEx.SelectNodes(doc,
                     "//*[@id='work_left']//div[contains(@class,'product-slider-data')]//div[@data-src or @data-thumb]")
                 ?? Enumerable.Empty<HtmlNode>())
        {
            var u = NetUrlHelper.Abs(HtmlEx.GetAttr(node, "data-src"), detailUrl);
            if (string.IsNullOrWhiteSpace(u)) u = NetUrlHelper.Abs(HtmlEx.GetAttr(node, "data-thumb"), detailUrl);
            ImageUrlNormalizer.AddThumb(meta, u);
        }

        // Clean up duplicate thumbnails
        meta.Thumbnails = meta.Thumbnails
            .Where(t => !string.IsNullOrEmpty(t) && !NetUrlHelper.Eq(t, meta.Primary) && !NetUrlHelper.Eq(t, meta.Backdrop))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Extract rating (0-5 scale) via AJAX API
        try
        {
            var site = detailUrl.Contains("/pro/", StringComparison.OrdinalIgnoreCase) ? "pro" : "maniax";
            var ajaxUrl = $"https://www.dlsite.com/{site}/product/info/ajax?product_id={Uri.EscapeDataString(id)}";

            var json = await _net.GetJsonAsync(ajaxUrl,
                new Dictionary<string, string>
                {
                    { "X-Requested-With", "XMLHttpRequest" },
                    { "Referer", detailUrl }
                }, ct);

            if (json.RootElement.TryGetProperty(id, out var entry)
                && entry.TryGetProperty("rate_average_2dp", out var v)
                && v.ValueKind == JsonValueKind.Number
                && v.TryGetDouble(out var score))
            {
                meta.Rating = score; // Already in 0-5 scale
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "[DLsite] Rating fetch failed for product: {Id}", id);
        }

        return meta;
    }
}
