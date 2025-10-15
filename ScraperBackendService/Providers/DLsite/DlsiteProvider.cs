using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ScraperBackendService.Core.Abstractions;
using ScraperBackendService.Core.Net;
using ScraperBackendService.Core.Logging;
using ScraperBackendService.Core.Normalize;
using ScraperBackendService.Core.Parsing;
using ScraperBackendService.Core.Routing;
using ScraperBackendService.Core.Util;
using ScraperBackendService.Models;
using System.Globalization;
using NetUrlHelper = ScraperBackendService.Core.Net.UrlHelper;

namespace ScraperBackendService.Providers.DLsite;

/// <summary>
/// DLsite content provider for scraping doujin and commercial adult content metadata.
/// Supports both Maniax and Pro sites with unified search and comprehensive detail extraction.
/// Implements multi-site fallback strategy and advanced metadata parsing capabilities.
/// </summary>
/// <remarks>
/// This provider handles:
/// - Unified search across DLsite's movie/video content
/// - Multi-site detail extraction (Maniax and Pro sites)
/// - Japanese content with proper text normalization
/// - Rich metadata including personnel, genres, ratings, and images
/// - AJAX-based rating extraction for accurate scores
/// - Proper resource management and error handling
/// </remarks>
/// <example>
/// Usage example:
/// var provider = new DlsiteProvider(networkClient, logger);
/// 
/// // Search for Japanese content
/// var searchResults = await provider.SearchAsync("恋爱", 10, cancellationToken);
/// foreach (var hit in searchResults)
/// {
///     Console.WriteLine($"Found: {hit.Title} - {hit.DetailUrl}");
/// }
/// 
/// // Get detailed information
/// var details = await provider.FetchDetailAsync("https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html", cancellationToken);
/// if (details != null)
/// {
///     Console.WriteLine($"Title: {details.Title}");
///     Console.WriteLine($"Studio: {string.Join(", ", details.Studios)}");
///     Console.WriteLine($"Rating: {details.Rating}/5");
///     Console.WriteLine($"Personnel: {details.People.Count} entries");
/// }
/// </example>
public sealed class DlsiteProvider : IMediaProvider
{
    private readonly INetworkClient _net;
    private readonly ILogger<DlsiteProvider> _log;

    /// <summary>
    /// Initializes a new instance of the DlsiteProvider.
    /// </summary>
    /// <param name="net">Network client for HTTP operations</param>
    /// <param name="log">Logger for tracking operations and errors</param>
    public DlsiteProvider(INetworkClient net, ILogger<DlsiteProvider> log)
    {
        _net = net;
        _log = log;
    }

    /// <summary>
    /// Gets the provider name identifier.
    /// </summary>
    public string Name => "DLsite";

    /// <summary>
    /// Attempts to parse a DLsite product ID from the given input string.
    /// Supports both RJ (Maniax) and VJ (Pro) prefixed product identifiers.
    /// </summary>
    /// <param name="input">Input string that may contain a DLsite ID (URL or direct ID)</param>
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
    /// 
    /// // Parse VJ (Pro site) ID
    /// if (provider.TryParseId("VJ123456", out var id3))
    /// {
    ///     Console.WriteLine($"Pro ID: {id3}"); // Output: "VJ123456"
    /// }
    /// </example>
    public bool TryParseId(string input, out string id) => IdParsers.TryParseDlsiteId(input, out id);

    /// <summary>
    /// Builds a detail page URL from a DLsite product ID.
    /// Prefers Maniax site for broader content access and better compatibility.
    /// </summary>
    /// <param name="id">DLsite product ID (e.g., "RJ123456", "VJ123456")</param>
    /// <returns>Complete URL to the detail page on the appropriate DLsite section</returns>
    /// <example>
    /// var detailUrl = provider.BuildDetailUrlById("RJ123456");
    /// // Returns: "https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html"
    /// 
    /// var detailUrl2 = provider.BuildDetailUrlById("VJ123456");
    /// // Returns: "https://www.dlsite.com/maniax/work/=/product_id/VJ123456.html" (prefers Maniax)
    /// </example>
    public string BuildDetailUrlById(string id)
        => IdParsers.BuildDlsiteDetailUrl(id, preferManiax: true);

    /// <summary>
    /// Unified search URL template for DLsite content targeting movie/video category.
    /// Focuses on audiovisual content relevant to media library applications.
    /// </summary>
    private const string UnifiedSearchUrl =
        "https://www.dlsite.com/maniax/fsr/=/keyword/{0}/work_type_category[0]/movie/";

    /// <summary>
    /// Searches for DLsite content using the provided keyword with intelligent result extraction.
    /// Performs keyword normalization and extracts product information from search results.
    /// </summary>
    /// <param name="keyword">Search keyword or phrase (supports Japanese text)</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <param name="ct">Cancellation token for operation timeout</param>
    /// <returns>Read-only list of search hits containing URLs, titles, and cover images</returns>
    /// <remarks>
    /// This method:
    /// 1. Normalizes the search keyword for DLsite's search engine
    /// 2. Constructs search URL targeting movie/video content
    /// 3. Parses search results HTML to extract product links
    /// 4. Deduplicates results and validates product URLs
    /// 5. Extracts basic metadata (title, cover image) when available
    /// </remarks>
    /// <example>
    /// // Search with Japanese keyword
    /// var results = await provider.SearchAsync("恋爱", 5, CancellationToken.None);
    /// foreach (var hit in results)
    /// {
    ///     Console.WriteLine($"Title: {hit.Title}");
    ///     Console.WriteLine($"URL: {hit.DetailUrl}");
    ///     if (!string.IsNullOrEmpty(hit.CoverUrl))
    ///         Console.WriteLine($"Cover: {hit.CoverUrl}");
    /// }
    ///
    /// // Search with product ID (will find exact match)
    /// var results2 = await provider.SearchAsync("RJ123456", 1, CancellationToken.None);
    /// 
    /// // Search with English keyword
    /// var results3 = await provider.SearchAsync("voice", 10, CancellationToken.None);
    /// </example>
    public async Task<IReadOnlyList<ScraperBackendService.Core.Abstractions.SearchHit>> SearchAsync(string keyword, int maxResults, CancellationToken ct)
    {
        var normalized = TextNormalizer.NormalizeKeyword(keyword);
        var searchUrl = string.Format(CultureInfo.InvariantCulture, UnifiedSearchUrl, Uri.EscapeDataString(normalized));

        var html = await _net.GetHtmlAsync(searchUrl, ct);
        
        // HtmlDocument doesn't implement IDisposable, so we can't use 'using'
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
            if (!seen.Add(detailUrl)) continue; // Skip duplicates

            string title = a.GetAttributeValue("title", "") ?? "";
            string? cover = a.SelectSingleNode(".//img[@src]")?.GetAttributeValue("src", "");

            hits.Add(new SearchHit(detailUrl, ScrapingUtils.Clean(title), cover ?? ""));
            if (maxResults > 0 && hits.Count >= maxResults) break;
        }

        // Log search results
        if (hits.Count > 0)
        {
            _log.LogSuccess("DLsiteSearch", keyword, hits.Count);
        }
        else
        {
            _log.LogDebug("DLsiteSearch", "No search hits found", keyword);
        }

        return hits;
    }

    /// <summary>
    /// DLsite work detail URL templates for different site sections.
    /// Maniax handles most doujin content, Pro handles commercial releases.
    /// </summary>
    private const string ManiaxWorkUrl = "https://www.dlsite.com/maniax/work/=/product_id/{0}.html";
    private const string ProWorkUrl = "https://www.dlsite.com/pro/work/=/product_id/{0}.html";

    /// <summary>
    /// Fetches detailed metadata for a specific DLsite product with multi-site fallback strategy.
    /// Attempts multiple site sections (Maniax, Pro) to locate and extract comprehensive product information.
    /// </summary>
    /// <param name="detailUrl">URL of the detail page to scrape</param>
    /// <param name="ct">Cancellation token for operation timeout</param>
    /// <returns>Complete metadata object or null if extraction fails</returns>
    /// <remarks>
    /// This method implements a fallback strategy:
    /// 1. Extracts product ID from the provided URL
    /// 2. Attempts to fetch from Maniax site (broader content access)
    /// 3. Falls back to Pro site if Maniax fails
    /// 4. Ensures original URL is preserved in SourceUrls for reference
    /// 5. Returns null only if all attempts fail
    /// </remarks>
    /// <example>
    /// // Fetch from specific URL
    /// var metadata = await provider.FetchDetailAsync("https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html", CancellationToken.None);
    /// if (metadata != null)
    /// {
    ///     Console.WriteLine($"Title: {metadata.Title}");
    ///     Console.WriteLine($"Description: {metadata.Description}");
    ///     Console.WriteLine($"Rating: {metadata.Rating}/5");
    ///     Console.WriteLine($"Studio: {string.Join(", ", metadata.Studios)}");
    ///     Console.WriteLine($"Genres: {string.Join(", ", metadata.Genres)}");
    ///     Console.WriteLine($"Personnel: {metadata.People.Count} people");
    ///     Console.WriteLine($"Images: {metadata.Thumbnails.Count} thumbnails");
    ///     Console.WriteLine($"Release Date: {metadata.ReleaseDate}");
    /// }
    /// else
    /// {
    ///     Console.WriteLine("Product not found or access denied");
    /// }
    /// </example>
    public async Task<Metadata?> FetchDetailAsync(string detailUrl, CancellationToken ct)
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
                if (meta != null) 
                {
                    // Ensure the original detailUrl is included in SourceUrls for reference
                    if (!meta.SourceUrls.Contains(detailUrl))
                    {
                        meta.SourceUrls.Add(detailUrl);
                    }
                    return meta;
                }
            }
            catch (Exception ex)
            {
                last = ex;
                _log.LogDebug("DLsiteDetail", "Detail parsing failed", url, ex);
            }
        }

        if (last != null) _log.LogWarning("DLsiteDetail", "All detail parsing attempts failed", detailUrl, last);
        return null;
    }

    /// <summary>
    /// Parses a single DLsite detail page to extract comprehensive metadata.
    /// Handles complete product information including descriptions, genres, personnel, images, and ratings.
    /// Implements advanced parsing techniques for Japanese content and DLsite-specific structures.
    /// </summary>
    /// <param name="detailUrl">URL of the detail page to parse</param>
    /// <param name="ct">Cancellation token for operation timeout</param>
    /// <returns>Complete metadata object or null if parsing fails or content not found</returns>
    /// <remarks>
    /// This method performs comprehensive parsing:
    /// 1. Validates page content using key DLsite elements
    /// 2. Extracts product title and cleans formatting
    /// 3. Parses rich text descriptions with length limits
    /// 4. Extracts studio/circle information from maker tables
    /// 5. Processes series information
    /// 6. Extracts and normalizes genre/tag information
    /// 7. Parses Japanese release dates (yyyy年M月d日 format)
    /// 8. Extracts personnel using specialized DLsite parsing
    /// 9. Processes product images (primary, backdrop, thumbnails)
    /// 10. Fetches accurate ratings via AJAX API
    /// 11. Applies title cleaning for frontend display
    /// </remarks>
    /// <example>
    /// var metadata = await ParseDetailPageAsync("https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html", CancellationToken.None);
    /// if (metadata != null)
    /// {
    ///     Console.WriteLine($"Product ID: {metadata.ID}");
    ///     Console.WriteLine($"Extracted {metadata.Genres.Count} genres");
    ///     Console.WriteLine($"Found {metadata.People.Count} personnel entries");
    ///     Console.WriteLine($"Description length: {metadata.Description?.Length ?? 0} chars");
    ///     Console.WriteLine($"Primary image: {!string.IsNullOrEmpty(metadata.Primary)}");
    ///     Console.WriteLine($"Rating available: {metadata.Rating.HasValue}");
    ///     if (metadata.ReleaseDate.HasValue)
    ///         Console.WriteLine($"Release date: {metadata.ReleaseDate.Value:yyyy-MM-dd}");
    /// }
    /// </example>
    private async Task<Metadata?> ParseDetailPageAsync(string detailUrl, CancellationToken ct)
    {
        var id = IdParsers.ParseIdFromDlsiteUrl(detailUrl);
        if (string.IsNullOrEmpty(id)) return null;

        var html = await _net.GetHtmlAsync(detailUrl, ct);
        if (string.IsNullOrWhiteSpace(html)) return null;

        // HtmlDocument doesn't implement IDisposable, so we can't use 'using'
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
            _log.LogWarning("ParseDetailPage", "No valid DLsite product found", detailUrl);
            return null;
        }

        var meta = new Metadata
        {
            ID = id,
            SourceUrls = new List<string>()
        };
        
        // Ensure SourceUrls is properly initialized and add current URL
        meta.SourceUrls.Add(detailUrl);

        // Extract title from main product name element
        meta.Title = TextNormalizer.Clean(HtmlEx.SelectText(doc, "//h1[@id='work_name']"));

        // Extract description with rich text support and reasonable limits
        var descRoot = HtmlEx.SelectSingle(doc, "//div[@itemprop='description' and contains(@class,'work_parts_container')]");
        meta.Description = RichTextExtractor.ExtractFrom(descRoot, new RichTextOptions
        {
            MaxChars = 1600,
            MaxParagraphs = 10
        });

        // Extract studio/circle information from maker table
        var maker = HtmlEx.ExtractOutlineCell(doc,
            "//table[@id='work_maker']//tr[.//th[contains(normalize-space(.),'ブランド名') or contains(normalize-space(.),'サークル名')]]//td",
            TextNormalizer.Clean);
        if (!string.IsNullOrWhiteSpace(maker)) meta.Studios.Add(maker);

        // Extract series information from outline table
        var series = HtmlEx.ExtractOutlineCell(doc,
            "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'シリーズ')]]//td",
            TextNormalizer.Clean);
        if (!string.IsNullOrWhiteSpace(series)) meta.Series.Add(series);

        // Extract genres/tags from genre section
        foreach (var a in HtmlEx.SelectNodes(doc,
                     "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'ジャンル')]]//td//div[contains(@class,'main_genre')]//a")
                 ?? Enumerable.Empty<HtmlNode>())
        {
            var g = TextNormalizer.Clean(a.InnerText);
            if (!string.IsNullOrEmpty(g) && !meta.Tags.Contains(g))
                meta.Tags.Add(g);
        }

        // Extract release date (Japanese format: yyyy年M月d日)
        var rawDate = HtmlEx.ExtractOutlineCellPreferA(doc,
            "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'販売日')]]//td",
            TextNormalizer.Clean);
        var jp = DateTimeNormalizer.ParseJapaneseYmd(rawDate);
        if (jp is { } dt) { meta.ReleaseDate = dt; meta.Year = dt.Year; }

        // Extract personnel information (voice actors, directors, etc.) using specialized DLsite parsing
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

        // Extract first thumbnail as backdrop fallback
        var firstThumbNode = HtmlEx.SelectSingle(doc,
            "//*[@id='work_left']//div[contains(@class,'product-slider-data')]/div[1]");
        var firstThumbUrl = NetUrlHelper.Abs(HtmlEx.GetAttr(firstThumbNode, "data-thumb"), detailUrl);
        if (!string.IsNullOrEmpty(firstThumbUrl))
        {
            meta.Backdrop = firstThumbUrl;
            ImageUrlNormalizer.AddThumb(meta, firstThumbUrl);
        }

        // Extract all thumbnail images from slider data
        foreach (var node in HtmlEx.SelectNodes(doc,
                     "//*[@id='work_left']//div[contains(@class,'product-slider-data')]//div[@data-src or @data-thumb]")
                 ?? Enumerable.Empty<HtmlNode>())
        {
            var u = NetUrlHelper.Abs(HtmlEx.GetAttr(node, "data-src"), detailUrl);
            if (string.IsNullOrWhiteSpace(u)) u = NetUrlHelper.Abs(HtmlEx.GetAttr(node, "data-thumb"), detailUrl);
            ImageUrlNormalizer.AddThumb(meta, u);
        }

        // Clean up duplicate thumbnails and avoid using primary/backdrop as thumbnails
        meta.Thumbnails = meta.Thumbnails
            .Where(t => !string.IsNullOrEmpty(t) && !NetUrlHelper.Eq(t, meta.Primary) && !NetUrlHelper.Eq(t, meta.Backdrop))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Extract rating (0-5 scale) via AJAX API for accurate scoring
        try
        {
            var site = detailUrl.Contains("/pro/", StringComparison.OrdinalIgnoreCase) ? "pro" : "maniax";
            var ajaxUrl = $"https://www.dlsite.com/{site}/product/info/ajax?product_id={Uri.EscapeDataString(id)}";

            using var json = await _net.GetJsonAsync(ajaxUrl,
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
                meta.Rating = score; // Already in 0-5 scale from DLsite API
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug("RatingFetch", "Rating fetch failed for product", id, ex);
        }

        // Clean titles before returning to frontend
        if (!string.IsNullOrWhiteSpace(meta.Title))
        {
            meta.Title = TitleCleaner.CleanTitle(meta.Title);
        }

        if (!string.IsNullOrWhiteSpace(meta.OriginalTitle))
        {
            meta.OriginalTitle = TitleCleaner.CleanTitle(meta.OriginalTitle);
        }

        return meta;
    }
}
