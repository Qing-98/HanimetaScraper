using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScraperBackendService.Core.Abstractions;
using ScraperBackendService.Core.Normalize;
using ScraperBackendService.Core.Parsing;
using ScraperBackendService.Core.Routing;
using ScraperBackendService.Core.Util;
using ScraperBackendService.Models;

namespace ScraperBackendService.Providers.Hanime;

/// <summary>
/// Hanime content provider for scraping anime content metadata.
/// Supports both HTTP and Playwright-based scraping approaches.
/// </summary>
/// <example>
/// Usage example:
/// var provider = new HanimeProvider(networkClient, logger);
/// var searchResults = await provider.SearchAsync("Love", 10, cancellationToken);
/// var details = await provider.FetchDetailAsync("https://hanime1.me/watch?v=12345", cancellationToken);
/// </example>
public sealed class HanimeProvider : IMediaProvider
{
    private readonly INetworkClient _net;
    private readonly ILogger<HanimeProvider> _log;

    private const string Host = "https://hanime1.me";

    public HanimeProvider(INetworkClient net, ILogger<HanimeProvider> log)
    {
        _net = net;
        _log = log;
    }

    public string Name => "Hanime";

    /// <summary>
    /// Attempts to parse a Hanime ID from the given input string.
    /// </summary>
    /// <param name="input">Input string that may contain a Hanime ID</param>
    /// <param name="id">Extracted Hanime ID if successful</param>
    /// <returns>True if ID was successfully parsed, false otherwise</returns>
    /// <example>
    /// // Parse from URL
    /// if (provider.TryParseId("https://hanime1.me/watch?v=12345", out var id))
    /// {
    ///     Console.WriteLine($"Extracted ID: {id}"); // Output: "12345"
    /// }
    ///
    /// // Parse from direct ID
    /// if (provider.TryParseId("86994", out var id2))
    /// {
    ///     Console.WriteLine($"Direct ID: {id2}"); // Output: "86994"
    /// }
    /// </example>
    public bool TryParseId(string input, out string id) => IdParsers.TryParseHanimeId(input, out id);

    /// <summary>
    /// Builds a detail page URL from a Hanime ID.
    /// </summary>
    /// <param name="id">Hanime content ID</param>
    /// <returns>Complete URL to the detail page</returns>
    /// <example>
    /// var detailUrl = provider.BuildDetailUrlById("12345");
    /// // Returns: "https://hanime1.me/watch?v=12345"
    /// </example>
    public string BuildDetailUrlById(string id) => IdParsers.BuildHanimeDetailUrl(id);

    /// <summary>
    /// Searches for Hanime content using the provided keyword.
    /// Returns a list of search hits with basic information.
    /// </summary>
    /// <param name="keyword">Search keyword or phrase</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of search hits containing URLs, titles, and cover images</returns>
    /// <example>
    /// // Search for anime containing "Love"
    /// var results = await provider.SearchAsync("Love", 5, CancellationToken.None);
    /// foreach (var hit in results)
    /// {
    ///     Console.WriteLine($"Title: {hit.Title}");
    ///     Console.WriteLine($"URL: {hit.DetailUrl}");
    ///     Console.WriteLine($"Cover: {hit.CoverUrl}");
    /// }
    /// </example>
    public async Task<IReadOnlyList<ScraperBackendService.Core.Abstractions.SearchHit>> SearchAsync(string keyword, int maxResults, CancellationToken ct)
    {
        var sort = "最新上市"; // Latest releases
        var queryParam = $"?query={Uri.EscapeDataString(keyword)}";
        var sortParam = $"&sort={Uri.EscapeDataString(sort)}";
        var searchUrl = $"{Host}/search{queryParam}{sortParam}";

        IPage? page = null;
        try
        {
            page = await _net.OpenPageAsync(searchUrl, ct);
            if (page is null)
            {
                // Fallback to HTML parsing if Playwright is not available
                var html = await _net.GetHtmlAsync(searchUrl, ct);
                return ParseSearchFromHtml(html, searchUrl, maxResults);
            }

            // Use Playwright for dynamic content extraction
            var itemLocator = page.Locator("div[title] >> a.overlay");
            
            // Check if any items exist before waiting, with a short timeout
            try
            {
                await itemLocator.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
            }
            catch (TimeoutException)
            {
                // No search results found, return empty list
                _log.LogInformation("No search results found for keyword: {Keyword}", keyword);
                return new List<ScraperBackendService.Core.Abstractions.SearchHit>();
            }

            var hits = new List<ScraperBackendService.Core.Abstractions.SearchHit>();
            var baseUri = new Uri(Host);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int count = await itemLocator.CountAsync();
            for (int i = 0; i < count && hits.Count < maxResults; i++)
            {
                var a = itemLocator.Nth(i);
                string? href = await a.GetAttributeAsync("href");
                if (string.IsNullOrWhiteSpace(href)) continue;

                var detailUrl = new Uri(baseUri, href).AbsoluteUri;
                if (!detailUrl.StartsWith($"{Host}/watch", StringComparison.OrdinalIgnoreCase)) continue;
                if (!seen.Add(detailUrl)) continue;

                var container = a.Locator("xpath=ancestor::div[@title][1]");
                string title = (await container.GetAttributeAsync("title"))?.Trim() ?? "";

                string cover = "";
                try
                {
                    var img = container.Locator("img[src*='/thumbnail/']");
                    if (await img.CountAsync() > 0)
                        cover = await img.First.GetAttributeAsync("src") ?? "";
                }
                catch { /* Ignore image extraction errors */ }

                hits.Add(new ScraperBackendService.Core.Abstractions.SearchHit(detailUrl, TextNormalizer.Clean(title), cover));
            }

            // Log search results
            if (hits.Count > 0)
            {
                _log.LogInformation("[Hanime] Search completed: {Keyword}, found {Count} hits", keyword, hits.Count);
            }
            else
            {
                _log.LogInformation("[Hanime] Search completed: {Keyword}, no search hits found", keyword);
            }

            return hits;
        }
        catch (TimeoutException ex)
        {
            // Handle timeout gracefully by falling back to HTML parsing
            _log.LogWarning("Playwright timeout during search for keyword: {Keyword}, falling back to HTML parsing. Error: {Error}", keyword, ex.Message);
            try
            {
                var html = await _net.GetHtmlAsync(searchUrl, ct);
                return ParseSearchFromHtml(html, searchUrl, maxResults);
            }
            catch (Exception fallbackEx)
            {
                _log.LogError(fallbackEx, "Both Playwright and HTML parsing failed for keyword: {Keyword}", keyword);
                return new List<ScraperBackendService.Core.Abstractions.SearchHit>();
            }
        }
        finally
        {
            await ClosePageAndContextAsync(page);
        }
    }

    /// <summary>
    /// Parses search results from HTML content when Playwright is not available.
    /// Fallback method for HTML-only parsing.
    /// </summary>
    /// <param name="html">HTML content of the search page</param>
    /// <param name="baseUrl">Base URL for resolving relative links</param>
    /// <param name="maxResults">Maximum number of results to extract</param>
    /// <returns>List of search hits parsed from HTML</returns>
    /// <example>
    /// var html = await httpClient.GetStringAsync("https://hanime1.me/search?query=Love");
    /// var results = ParseSearchFromHtml(html, "https://hanime1.me/search", 10);
    /// </example>
    private static IReadOnlyList<SearchHit> ParseSearchFromHtml(string html, string baseUrl, int maxResults)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var hits = new List<SearchHit>();
        var nodes = ScrapingUtils.SelectNodes(doc, "//a[contains(@class,'overlay') and starts-with(@href,'/watch')]");
        
        if (nodes == null || !nodes.Any())
        {
            // No search results found in HTML
            return hits;
        }

        foreach (var a in nodes)
        {
            try
            {
                var href = a.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href)) continue;

                var detailUrl = new Uri(new Uri(Host), href).AbsoluteUri;

                var container = a.SelectSingleNode("ancestor::div[@title][1]");
                var title = container?.GetAttributeValue("title", "") ?? a.GetAttributeValue("title", "") ?? "";
                var cover = container?.SelectSingleNode(".//img[@src]")?.GetAttributeValue("src", "") ?? "";

                hits.Add(new SearchHit(detailUrl, TextNormalizer.Clean(title), cover));
                if (maxResults > 0 && hits.Count >= maxResults) break;
            }
            catch (Exception)
            {
                // Skip invalid entries and continue processing
                continue;
            }
        }
        return hits;
    }

    /// <summary>
    /// Fetches detailed metadata for a specific Hanime content.
    /// Extracts comprehensive information including title, description, tags, rating, and images.
    /// </summary>
    /// <param name="detailUrl">URL of the detail page to scrape</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Complete metadata object or null if extraction fails</returns>
    /// <example>
    /// var metadata = await provider.FetchDetailAsync("https://hanime1.me/watch?v=12345", CancellationToken.None);
    /// if (metadata != null)
    /// {
    ///     Console.WriteLine($"Title: {metadata.Title}");
    ///     Console.WriteLine($"Description: {metadata.Description}");
    ///     Console.WriteLine($"Rating: {metadata.Rating}/5");
    ///     Console.WriteLine($"Tags: {string.Join(", ", metadata.Genres)}");
    ///     Console.WriteLine($"Cover: {metadata.Primary}");
    /// }
    /// </example>
    public async Task<HanimeMetadata?> FetchDetailAsync(string detailUrl, CancellationToken ct)
    {
        IPage? page = null;
        try
        {
            page = await _net.OpenPageAsync(detailUrl, ct);
            string html;
            var seedMeta = new HanimeMetadata { SourceUrls = new List<string>() };

            if (page is null)
            {
                html = await _net.GetHtmlAsync(detailUrl, ct);
            }
            else
            {
                // Extract title using Playwright locators for better accuracy
                await TryFillTitleViaLocatorAsync(page, seedMeta, ct);
                html = await page.ContentAsync();
            }

            var meta = ParseDetailHtml(html, detailUrl, seedMeta);
            return meta;
        }
        finally
        {
            await ClosePageAndContextAsync(page);
        }
    }

    /// <summary>
    /// Attempts to extract the title using Playwright locators for enhanced accuracy.
    /// Used when Playwright is available for dynamic content handling.
    /// </summary>
    /// <param name="page">Playwright page object</param>
    /// <param name="meta">Metadata object to populate with title information</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    /// <example>
    /// await TryFillTitleViaLocatorAsync(page, metadata, CancellationToken.None);
    /// // metadata.Title and metadata.OriginalTitle will be populated if successful
    /// </example>
    private static async Task TryFillTitleViaLocatorAsync(IPage page, HanimeMetadata meta, CancellationToken ct)
    {
        try
        {
            var titleNode = page.Locator("#shareBtn-title");
            if (await titleNode.CountAsync() > 0)
            {
                var raw = (await titleNode.InnerTextAsync()).Trim();
                var re = new Regex(@"\[.*?\]");
                var clean = re.Replace(raw, string.Empty, 1 /*count*/).Trim();
                meta.OriginalTitle = clean;
                if (string.IsNullOrWhiteSpace(meta.Title)) meta.Title = clean;
            }
        }
        catch { /* Ignore title extraction errors */ }
    }

    /// <summary>
    /// Parses detailed metadata from HTML content of a Hanime detail page.
    /// Extracts title, description, tags, rating, studio, release date, and images.
    /// </summary>
    /// <param name="html">HTML content of the detail page</param>
    /// <param name="detailUrl">URL of the detail page</param>
    /// <param name="seed">Optional seed metadata to merge with parsed data</param>
    /// <returns>Complete metadata object with all extracted information, or null if no valid content found</returns>
    /// <example>
    /// var html = await httpClient.GetStringAsync("https://hanime1.me/watch?v=12345");
    /// var metadata = ParseDetailHtml(html, "https://hanime1.me/watch?v=12345", null);
    /// if (metadata != null)
    /// {
    ///     Console.WriteLine($"Extracted {metadata.Genres.Count} tags");
    ///     Console.WriteLine($"Rating: {metadata.Rating}/5");
    /// }
    /// </example>
    private HanimeMetadata? ParseDetailHtml(string html, string detailUrl, HanimeMetadata? seed)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // First, check if this is a valid Hanime content page
        // Look for key elements that indicate a valid content page
        var titleNode = doc.DocumentNode.SelectSingleNode("//h3[@id='shareBtn-title']");
        var videoPlayer = doc.DocumentNode.SelectSingleNode("//video[@poster]");
        var tagDivs = doc.DocumentNode.SelectNodes("//div[@class='single-video-tag']");

        // If we don't find any key content indicators, this is likely a 404 or invalid page
        if (titleNode == null && videoPlayer == null && (tagDivs == null || tagDivs.Count == 0))
        {
            _log.LogWarning("No valid Hanime content found at URL: {Url}", detailUrl);
            return null;
        }

        var meta = seed ?? new HanimeMetadata();
        if (!meta.SourceUrls.Contains(detailUrl)) meta.SourceUrls.Add(detailUrl);
        if (IdParsers.TryExtractHanimeIdFromUrl(detailUrl, out var id)) meta.ID = id;

        // Extract title - remove brackets and clean text
        if (titleNode != null)
        {
            var raw = titleNode.InnerText.Trim();
            var re = new Regex(@"\[.*?\]");
            var clean = re.Replace(raw, string.Empty, 1 /*count*/).Trim();
            meta.OriginalTitle = clean;
            if (string.IsNullOrWhiteSpace(meta.Title)) meta.Title = clean;
        }

        // Extract description - target specific description area
        var capDiv = doc.DocumentNode.SelectSingleNode("//div[@class='video-caption-text caption-ellipsis']");
        if (capDiv != null)
        {
            meta.Description = capDiv.InnerText?.Trim();
        }
        else
        {
            // Fallback approach for description
            capDiv = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'video-caption-text')]");
            if (capDiv != null)
            {
                meta.Description = capDiv.InnerText?.Trim();
            }
        }

        // Extract tags/genres
        var tagDivs2 = doc.DocumentNode.SelectNodes("//div[@class='single-video-tag' and not(@data-toggle) and not(@data-target)]");
        if (tagDivs2 != null)
        {
            var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var div in tagDivs2)
            {
                var a = div.SelectSingleNode("./a");
                string tag;
                if (a != null)
                {
                    var textNode = a.SelectSingleNode("text()");
                    tag = (textNode?.InnerText ?? a.InnerText) ?? "";
                }
                else tag = div.InnerText ?? "";

                tag = CleanTag(tag);
                if (!string.IsNullOrWhiteSpace(tag)) tagSet.Add(tag);
            }
            meta.Genres = tagSet.ToList();
        }

        // Extract rating: convert percentage to 0-5 scale
        var likeDiv = doc.DocumentNode.SelectSingleNode("//div[@id='video-like-form-wrapper']//div[contains(@class,'single-icon')]");
        if (likeDiv != null)
        {
            var text = string.Concat(likeDiv.ChildNodes.Where(n => n.NodeType == HtmlNodeType.Text).Select(n => n.InnerText)).Trim();
            var m = Regex.Match(text, @"(\d+)%");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var percent))
                meta.Rating = percent / 20.0; // Convert percentage to 0-5 scale
        }

        // Extract studio information
        var artistNode = doc.DocumentNode.SelectSingleNode("//a[@id='video-artist-name']");
        if (artistNode != null)
        {
            var studio = artistNode.InnerText.Trim();
            if (!string.IsNullOrWhiteSpace(studio) && !meta.Studios.Contains(studio))
                meta.Studios.Add(studio);
        }

        // Extract release date
        var descDiv = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'video-description-panel-hover')]");
        if (descDiv != null)
        {
            var m = Regex.Match(descDiv.InnerText, @"\d{4}-\d{2}-\d{2}");
            if (m.Success && DateTimeOffset.TryParse(m.Value, out var dt))
            { meta.ReleaseDate = dt; meta.Year = dt.Year; }
        }

        // Extract images - focus on video poster only
        if (videoPlayer != null)
        {
            var poster = videoPlayer.GetAttributeValue("poster", "");
            if (!string.IsNullOrWhiteSpace(poster))
            {
                // Use the same image as both Primary and Thumbnail
                meta.Primary = poster;
                meta.Thumbnails.Add(poster);
                // Leave Backdrop empty (not set)
            }
        }

        return meta;
    }

    /// <summary>
    /// Cleans tag text by removing special characters and normalizing whitespace.
    /// Removes quotes, colons, and HTML entities commonly found in tag text.
    /// </summary>
    /// <param name="raw">Raw tag text to clean</param>
    /// <returns>Cleaned tag text</returns>
    /// <example>
    /// var cleaned = CleanTag("\"Romance\": "); // Returns: "Romance"
    /// var cleaned2 = CleanTag("Comedy&nbsp;"); // Returns: "Comedy"
    /// </example>
    private static string CleanTag(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return raw.Replace("\"", "")
                  .Replace("“", "")
                  .Replace("”", "")
                  .Replace("：", "")
                  .Replace(":", "")
                  .Replace("\u00A0", "")
                  .Replace("&nbsp;", "")
                  .Trim();
    }

    /// <summary>
    /// Safely closes a Playwright page and its browser context.
    /// Handles cleanup even if the page or context is already closed.
    /// </summary>
    /// <param name="page">Playwright page to close (can be null)</param>
    /// <returns>Task representing the asynchronous cleanup operation</returns>
    /// <example>
    /// IPage? page = await browser.NewPageAsync();
    /// try
    /// {
    ///     // Use page...
    /// }
    /// finally
    /// {
    ///     await ClosePageAndContextAsync(page);
    /// }
    /// </example>
    private static async Task ClosePageAndContextAsync(IPage? page)
    {
        if (page is null) return;
        try
        {
            var ctx = page.Context;
            if (!page.IsClosed) await page.CloseAsync();
            await ctx.CloseAsync();
        }
        catch { /* Ignore cleanup errors */ }
    }
}
