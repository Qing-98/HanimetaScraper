using System.Globalization;
using System.Text.Json;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ScraperBackendService.Core.Abstractions;
using ScraperBackendService.Core.Normalize;
using ScraperBackendService.Core.Net;
using ScraperBackendService.Core.Parsing;
using ScraperBackendService.Core.Routing;
using ScraperBackendService.Models;
using System.Linq;

namespace ScraperBackendService.Providers.DLsite;

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

    // ===== 路由 =====
    public bool TryParseId(string input, out string id) => IdParsers.TryParseDlsiteId(input, out id);

    public string BuildDetailUrlById(string id)
        => IdParsers.BuildDlsiteDetailUrl(id, preferManiax: true);

    // ===== 搜索 =====
    private const string UnifiedSearchUrl =
        "https://www.dlsite.com/maniax/fsr/=/keyword/{0}/work_type_category[0]/movie/";

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string keyword, int maxResults, CancellationToken ct)
    {
        var normalized = TextNormalizer.NormalizeKeyword(keyword);
        var searchUrl = string.Format(CultureInfo.InvariantCulture, UnifiedSearchUrl, Uri.EscapeDataString(normalized));
        _log.LogInformation("[DLsite] Search: {Url}", searchUrl);

        var html = await _net.GetHtmlAsync(searchUrl, ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var hits = new List<SearchHit>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in HtmlEx.SelectNodes(doc, "//div[@id='search_result_list']//li//a[contains(@href,'/work/=/product_id/')]")
                 ?? Enumerable.Empty<HtmlNode>())
        {
            var href = a.GetAttributeValue("href", "");
            var abs = UrlHelper.Abs(href, searchUrl);
            if (!Uri.TryCreate(abs, UriKind.Absolute, out _)) continue;

            var id = IdParsers.ParseIdFromDlsiteUrl(abs);
            if (string.IsNullOrEmpty(id)) continue;

            var detailUrl = IdParsers.BuildDlsiteDetailUrl(id, preferManiax: true);
            if (!seen.Add(detailUrl)) continue;

            string? title = a.GetAttributeValue("title", null);
            if (string.IsNullOrWhiteSpace(title))
            {
                var tnode = a.SelectSingleNode(".//img[@alt]") ?? a.SelectSingleNode(".//span");
                title = tnode?.GetAttributeValue("alt", null) ?? tnode?.InnerText?.Trim();
            }

            string? cover = a.SelectSingleNode(".//img[@src]")?.GetAttributeValue("src", null);
            if (!string.IsNullOrWhiteSpace(cover))
                cover = ImageUrlNormalizer.EnsureJpg(UrlHelper.Abs(cover!, searchUrl));

            hits.Add(new SearchHit(detailUrl, TextNormalizer.Clean(title ?? ""), cover));

            if (maxResults > 0 && hits.Count >= maxResults) break;
        }

        return hits;
    }

    // ===== 详情 =====
    private const string ManiaxWorkUrl = "https://www.dlsite.com/maniax/work/=/product_id/{0}.html";
    private const string ProWorkUrl = "https://www.dlsite.com/pro/work/=/product_id/{0}.html";

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
                _log.LogDebug(ex, "[DLsite] detail failed: {Url}", url);
            }
        }
        if (last != null) _log.LogWarning(last, "[DLsite] all detail attempts failed.");
        return null;
    }

    private async Task<HanimeMetadata?> ParseDetailPageAsync(string detailUrl, CancellationToken ct)
    {
        var id = IdParsers.ParseIdFromDlsiteUrl(detailUrl);
        if (string.IsNullOrEmpty(id)) return null;

        var html = await _net.GetHtmlAsync(detailUrl, ct);
        if (string.IsNullOrWhiteSpace(html)) return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var meta = new HanimeMetadata
        {
            ID = id,
            SourceUrls = new List<string> { detailUrl }
        };

        // 标题
        meta.Title = TextNormalizer.Clean(HtmlEx.SelectText(doc, "//h1[@id='work_name']"));

        // 简介
        var descRoot = HtmlEx.SelectSingle(doc, "//div[@itemprop='description' and contains(@class,'work_parts_container')]");
        meta.Description = RichTextExtractor.ExtractFrom(descRoot, new RichTextOptions
        {
            MaxChars = 1600,
            MaxParagraphs = 10
        });

        // 厂牌
        var maker = HtmlEx.ExtractOutlineCell(doc,
            "//table[@id='work_maker']//tr[.//th[contains(normalize-space(.),'ブランド名') or contains(normalize-space(.),'サークル名')]]//td",
            TextNormalizer.Clean);
        if (!string.IsNullOrWhiteSpace(maker)) meta.Studios.Add(maker);

        // 系列
        var series = HtmlEx.ExtractOutlineCell(doc,
            "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'シリーズ')]]//td",
            TextNormalizer.Clean);
        if (!string.IsNullOrWhiteSpace(series)) meta.Series.Add(series);

        // Genres
        foreach (var a in HtmlEx.SelectNodes(doc,
                     "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'ジャンル')]]//td//div[contains(@class,'main_genre')]//a")
                 ?? Enumerable.Empty<HtmlNode>())
        {
            var g = TextNormalizer.Clean(a.InnerText);
            if (!string.IsNullOrEmpty(g) && !meta.Genres.Contains(g))
                meta.Genres.Add(g);
        }

        // 发布日期（yyyy年M月d日）
        var rawDate = HtmlEx.ExtractOutlineCellPreferA(doc,
            "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'販売日')]]//td",
            TextNormalizer.Clean);
        var jp = DateTimeNormalizer.ParseJapaneseYmd(rawDate);
        if (jp is { } dt) { meta.ReleaseDate = dt; meta.Year = dt.Year; }

        // 人员
        foreach (var tr in HtmlEx.SelectNodes(doc, "//table[@id='work_outline']//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var th = tr.SelectSingleNode(".//th");
            var td = tr.SelectSingleNode(".//td");
            if (th is null || td is null) continue;

            var roleRaw = TextNormalizer.Clean(th.InnerText);
            var names = td.SelectNodes(".//a")?.Select(x => TextNormalizer.Clean(x.InnerText))
                           .Where(s => !string.IsNullOrEmpty(s)).ToList()
                        ?? new List<string> { TextNormalizer.Clean(td.InnerText) };

            var (type, subRole) = MapStaffRole(roleRaw);
            foreach (var n in names)
            {
                if (!string.IsNullOrWhiteSpace(n))
                    meta.People.Add(new PersonDto { Name = n, Type = type, Role = subRole });
            }
        }

        // 图片
        var bigImgNode = HtmlEx.SelectSingle(doc,
            "//*[@id='work_left']//div[contains(@class,'work_slider_container')]//li[contains(@class,'slider_item') and contains(@class,'active')]//img");
        var bigPick = ImageUrlNormalizer.PickJpg(HtmlEx.GetAttr(bigImgNode, "src"), HtmlEx.GetAttr(bigImgNode, "srcset"), detailUrl);
        if (!string.IsNullOrEmpty(bigPick))
        {
            meta.Primary = bigPick;
            meta.Backdrop = bigPick;
        }

        var firstThumbNode = HtmlEx.SelectSingle(doc,
            "//*[@id='work_left']//div[contains(@class,'product-slider-data')]/div[1]");
        var firstThumbUrl = UrlHelper.Abs(HtmlEx.GetAttr(firstThumbNode, "data-thumb"), detailUrl);
        if (!string.IsNullOrEmpty(firstThumbUrl))
        {
            meta.Backdrop = firstThumbUrl;
            ImageUrlNormalizer.AddThumb(meta, firstThumbUrl);
        }

        foreach (var node in HtmlEx.SelectNodes(doc,
                     "//*[@id='work_left']//div[contains(@class,'product-slider-data')]//div[@data-src or @data-thumb]")
                 ?? Enumerable.Empty<HtmlNode>())
        {
            var u = UrlHelper.Abs(HtmlEx.GetAttr(node, "data-src"), detailUrl);
            if (string.IsNullOrWhiteSpace(u)) u = UrlHelper.Abs(HtmlEx.GetAttr(node, "data-thumb"), detailUrl);
            ImageUrlNormalizer.AddThumb(meta, u);
        }

        meta.Thumbnails = meta.Thumbnails
            .Where(t => !string.IsNullOrEmpty(t) && !UrlHelper.Eq(t, meta.Primary) && !UrlHelper.Eq(t, meta.Backdrop))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 评分（0–5）
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
                meta.Rating = score; // 0–5
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "[DLsite] rating fetch failed: {Id}", id);
        }

        return meta;
    }

    // 角色映射（可替换为独立 DlsiteRoleMapper）
    private static (string Type, string? Role) MapStaffRole(string header)
    {
        if (header.Contains("監督") || header.Contains("ディレクター")) return ("Director", null);
        if (header.Contains("シナリオ") || header.Contains("脚本")) return ("Writer", null);
        if (header.Contains("原画") || header.Contains("イラスト")) return ("Illustrator", null);
        if (header.Contains("制作") || header.Contains("企画") || header.Contains("プロデューサ")) return ("Producer", null);
        if (header.Contains("編集")) return ("Editor", null);
        if (header.Contains("音楽")) return ("Composer", null);
        if (header.Contains("声優") || header.Contains("出演者") || header.Contains("キャスト")) return ("Actor", null);
        return (header, null);
    }
}
