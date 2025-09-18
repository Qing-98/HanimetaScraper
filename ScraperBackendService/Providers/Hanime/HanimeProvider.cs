using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScraperBackendService.Core.Abstractions;
using ScraperBackendService.Core.Normalize;
using ScraperBackendService.Core.Parsing;
using ScraperBackendService.Core.Routing;
using ScraperBackendService.Models;

namespace ScraperBackendService.Providers.Hanime;

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

    // ===== 路由 =====
    public bool TryParseId(string input, out string id) => IdParsers.TryParseHanimeId(input, out id);
    public string BuildDetailUrlById(string id) => IdParsers.BuildHanimeDetailUrl(id);

    // ===== 搜索 =====
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string keyword, int maxResults, CancellationToken ct)
    {
        var sort = "最新上市";
        var queryParam = $"?query={Uri.EscapeDataString(keyword)}";
        var sortParam = $"&sort={Uri.EscapeDataString(sort)}";
        var searchUrl = $"{Host}/search{queryParam}{sortParam}";

        IPage? page = null;
        try
        {
            page = await _net.OpenPageAsync(searchUrl, ct);
            if (page is null)
            {
                var html = await _net.GetHtmlAsync(searchUrl, ct);
                return ParseSearchFromHtml(html, searchUrl, maxResults);
            }

            var itemLocator = page.Locator("div[title] >> a.overlay");
            await itemLocator.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

            var hits = new List<SearchHit>();
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
                string? title = (await container.GetAttributeAsync("title"))?.Trim();

                string? cover = null;
                try
                {
                    var img = container.Locator("img[src*='/thumbnail/']");
                    if (await img.CountAsync() > 0)
                        cover = await img.First.GetAttributeAsync("src");
                }
                catch { /* ignore */ }

                hits.Add(new SearchHit(detailUrl, TextNormalizer.Clean(title ?? ""), cover));
            }

            return hits;
        }
        finally
        {
            await ClosePageAndContextAsync(page);
        }
    }

    private static IReadOnlyList<SearchHit> ParseSearchFromHtml(string html, string baseUrl, int maxResults)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var hits = new List<SearchHit>();
        foreach (var a in doc.DocumentNode.SelectNodes("//a[contains(@class,'overlay') and starts-with(@href,'/watch')]")
                 ?? new HtmlNodeCollection(null))
        {
            var href = a.GetAttributeValue("href", "");
            var detailUrl = new Uri(new Uri(Host), href).AbsoluteUri;

            var container = a.SelectSingleNode("ancestor::div[@title][1]");
            var title = container?.GetAttributeValue("title", null) ?? a.GetAttributeValue("title", null);
            var cover = container?.SelectSingleNode(".//img[@src]")?.GetAttributeValue("src", null);

            hits.Add(new SearchHit(detailUrl, TextNormalizer.Clean(title ?? ""), cover));
            if (maxResults > 0 && hits.Count >= maxResults) break;
        }
        return hits;
    }

    // ===== 详情 =====
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
                await TryFillViaLocatorAsync(page, seedMeta, ct);
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

    private static async Task TryFillViaLocatorAsync(IPage page, HanimeMetadata meta, CancellationToken ct)
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

            var posterNode = page.Locator("video[poster]");
            if (await posterNode.CountAsync() > 0)
            {
                var poster = await posterNode.First.GetAttributeAsync("poster");
                if (!string.IsNullOrWhiteSpace(poster))
                {
                    if (string.IsNullOrWhiteSpace(meta.Primary)) meta.Primary = poster;
                    if (!meta.Thumbnails.Contains(poster)) meta.Thumbnails.Add(poster);
                }
            }
        }
        catch { /* ignore */ }
    }

    private HanimeMetadata ParseDetailHtml(string html, string detailUrl, HanimeMetadata? seed)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var meta = seed ?? new HanimeMetadata();
        if (!meta.SourceUrls.Contains(detailUrl)) meta.SourceUrls.Add(detailUrl);
        if (IdParsers.TryExtractHanimeIdFromUrl(detailUrl, out var id)) meta.ID = id;

        // 标题
        var titleNode = doc.DocumentNode.SelectSingleNode("//h3[@id='shareBtn-title']");
        if (titleNode != null)
        {
            var raw = titleNode.InnerText.Trim();
            var re = new Regex(@"\[.*?\]");
            var clean = re.Replace(raw, string.Empty, 1 /*count*/).Trim();
            meta.OriginalTitle = clean;
            if (string.IsNullOrWhiteSpace(meta.Title)) meta.Title = clean;
        }

        // 描述
        var capDiv = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'video-caption-text')]");
        meta.Description = RichTextExtractor.ExtractFrom(capDiv, new RichTextOptions { MaxChars = 2000 });

        // 标签
        var tagDivs = doc.DocumentNode.SelectNodes("//div[@class='single-video-tag' and not(@data-toggle) and not(@data-target)]");
        if (tagDivs != null)
        {
            var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var div in tagDivs)
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

        // 评分：百分比 → 0–5
        var likeDiv = doc.DocumentNode.SelectSingleNode("//div[@id='video-like-form-wrapper']//div[contains(@class,'single-icon')]");
        if (likeDiv != null)
        {
            var text = string.Concat(likeDiv.ChildNodes.Where(n => n.NodeType == HtmlNodeType.Text).Select(n => n.InnerText)).Trim();
            var m = Regex.Match(text, @"(\d+)%");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var percent))
                meta.Rating = percent / 20.0;
        }

        // Studio
        var artistNode = doc.DocumentNode.SelectSingleNode("//a[@id='video-artist-name']");
        if (artistNode != null)
        {
            var studio = artistNode.InnerText.Trim();
            if (!string.IsNullOrWhiteSpace(studio) && !meta.Studios.Contains(studio))
                meta.Studios.Add(studio);
        }

        // 日期
        var descDiv = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'video-description-panel-hover')]");
        if (descDiv != null)
        {
            var m = Regex.Match(descDiv.InnerText, @"\d{4}-\d{2}-\d{2}");
            if (m.Success && DateTimeOffset.TryParse(m.Value, out var dt))
            { meta.ReleaseDate = dt; meta.Year = dt.Year; }
        }

        // 图片/缩略
        if (string.IsNullOrWhiteSpace(meta.Primary))
        {
            var posterNode = doc.DocumentNode.SelectSingleNode("//video[@poster]");
            if (posterNode != null)
            {
                var poster = posterNode.GetAttributeValue("poster", "");
                if (!string.IsNullOrWhiteSpace(poster)) meta.Primary = poster;
            }
        }
        var thumbs = doc.DocumentNode.SelectNodes("//img[contains(@src,'/thumbnail/')]");
        if (thumbs != null)
        {
            foreach (var img in thumbs)
            {
                var u = img.GetAttributeValue("src", "");
                if (!string.IsNullOrWhiteSpace(u) && !meta.Thumbnails.Contains(u))
                    meta.Thumbnails.Add(u);
            }
        }

        return meta;
    }

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

    private static async Task ClosePageAndContextAsync(IPage? page)
    {
        if (page is null) return;
        try
        {
            var ctx = page.Context;
            if (!page.IsClosed) await page.CloseAsync();
            await ctx.CloseAsync();
        }
        catch { /* ignore */ }
    }
}
