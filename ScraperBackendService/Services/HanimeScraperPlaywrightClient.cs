using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScraperBackendService.AntiCloudflare;
using ScraperBackendService.Models;

namespace ScraperBackendService.Services
{
    public enum HanimeScrapeRoute
    {
        Auto = 0,
        ById = 1,
        ByFilename = 2
    }

    public class HanimeScraperPlaywrightClient
    {
        private readonly ILogger<HanimeScraperPlaywrightClient> _logger;
        private readonly PlaywrightContextManager _ctxMgr;

        private readonly SemaphoreSlim _detailConcurrency; // 控制同时打开的详情页数量
        private readonly Random _rnd = new();

        private const int MAX_DETAIL_CONCURRENCY = 3;
        private const string Host = "https://hanime1.me";

        public HanimeScraperPlaywrightClient(
            IBrowser browser,
            ILogger<HanimeScraperPlaywrightClient> logger,
            ScrapeRuntimeOptions? opt = null)
        {
            _logger = logger;
            _ctxMgr = new PlaywrightContextManager(browser, logger, opt);
            _detailConcurrency = new SemaphoreSlim(MAX_DETAIL_CONCURRENCY);
        }

        // ===================== 对外主入口 =====================
        public async Task<List<HanimeMetadata>> FetchAsync(
            string input,
            HanimeScrapeRoute route,
            int maxResults = 12,
            string? sort = "最新上市",
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            switch (route)
            {
                case HanimeScrapeRoute.ById:
                    if (!TryParseHanimeNumericId(input, out var id))
                        throw new ArgumentException($"按ID模式解析失败，输入不是合法的 Hanime 数字ID（支持：纯数字 或 {Host}/watch?v=12345）。输入：{input}");
                    _logger.LogInformation("Hanime 按ID直达: {Id}", id);
                    return await FetchByIdAsync(id, ct);

                case HanimeScrapeRoute.ByFilename:
                    _logger.LogInformation("Hanime 按文件名搜索: {Input}", input);
                    return await SearchByFilenameAsync(input, maxResults, sort, ct);

                case HanimeScrapeRoute.Auto:
                default:
                    if (TryParseHanimeNumericId(input, out var id2))
                    {
                        _logger.LogInformation("Hanime Auto→按ID直达: {Id}", id2);
                        return await FetchByIdAsync(id2, ct);
                    }
                    _logger.LogInformation("Hanime Auto→按文件名搜索: {Input}", input);
                    return await SearchByFilenameAsync(input, maxResults, sort, ct);
            }
        }

        public Task<List<HanimeMetadata>> FetchAsync(
            string input,
            HanimeScrapeRoute route,
            int maxResults = 12,
            string? sort = "最新上市")
            => FetchAsync(input, route, maxResults, sort, CancellationToken.None);

        public Task<List<HanimeMetadata>> SmartSearchAndFetchAllAsync(
            string searchTitle,
            int maxResults = 12,
            string? sort = "最新上市",
            CancellationToken ct = default)
            => FetchAsync(searchTitle, HanimeScrapeRoute.Auto, maxResults, sort, ct);

        public Task<List<HanimeMetadata>> SmartSearchAndFetchAllAsync(
            string searchTitle,
            int maxResults = 12,
            string? sort = "最新上市")
            => SmartSearchAndFetchAllAsync(searchTitle, maxResults, sort, CancellationToken.None);

        // ===================== 按ID直达 =====================
        public async Task<List<HanimeMetadata>> FetchByIdAsync(string numericId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(numericId) || !HanimeBareNumericIdRegex.IsMatch(numericId))
                throw new ArgumentException("Hanime ID 非法（需为纯数字字符串，长度≥3）。", nameof(numericId));

            var url = BuildDetailUrlById(numericId);
            _logger.LogInformation("Hanime 直达URL: {Url}", url);
            return await DirectFetchAsync(url, ct);
        }

        public Task<List<HanimeMetadata>> FetchByIdAsync(string numericId)
            => FetchByIdAsync(numericId, CancellationToken.None);

        // ===================== 搜索流程（Locator + 详情并发） =====================
        public async Task<List<HanimeMetadata>> SearchByFilenameAsync(
            string filenameOrText,
            int maxResults = 12,
            string? sort = "最新上市",
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            string keyword = BuildQueryFromFilename(filenameOrText);
            if (string.IsNullOrWhiteSpace(keyword))
                keyword = filenameOrText?.Trim() ?? string.Empty;

            string sortParam = string.IsNullOrWhiteSpace(sort) ? "" : $"&sort={Uri.EscapeDataString(sort)}";
            string queryParam = $"?query={Uri.EscapeDataString(keyword)}";
            string url = $"{Host}/search{queryParam}{sortParam}";

            var searchCtx = await _ctxMgr.GetOrCreateContextAsync(forDetail: false);
            var page = await searchCtx.NewPageAsync();
            _ctxMgr.BumpOpenedPages(searchCtx, forDetail: false);

            try
            {
                _logger.LogInformation("Hanime 搜索: {Url}", url);
                await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });

                var itemLocator = page.Locator("div[title] >> a.overlay");
                await itemLocator.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

                await DoAntiBotActionsAsync(page, ct);

                int count = await itemLocator.CountAsync();
                var results = new List<HanimeMetadata>();
                var baseUri = new Uri(Host);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < count && results.Count < maxResults; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var a = itemLocator.Nth(i);
                    string? href = await a.GetAttributeAsync("href");
                    if (string.IsNullOrWhiteSpace(href)) continue;

                    if (!Uri.TryCreate(baseUri, href, out var absUri)) continue;
                    var absUrl = absUri.AbsoluteUri;
                    if (!absUrl.StartsWith($"{Host}/watch", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!seen.Add(absUrl)) continue;

                    var container = a.Locator("xpath=ancestor::div[@title][1]");
                    string title = (await container.GetAttributeAsync("title"))?.Trim() ?? "";

                    string? cover = null;
                    try
                    {
                        var img = container.Locator("img[src*='/thumbnail/']");
                        if (await img.CountAsync() > 0)
                            cover = await img.First.GetAttributeAsync("src");
                    }
                    catch { /* ignore */ }

                    var meta = new HanimeMetadata { Title = title };
                    meta.SourceUrls.Add(absUrl);
                    TryExtractId(absUrl, out var id);
                    meta.ID = id;
                    if (!string.IsNullOrWhiteSpace(cover)) meta.Primary = cover;

                    results.Add(meta);
                }

                // 并发抓详情（forDetail=true）
                var tasks = results.Select(m => ProcessDetailAsync(m.SourceUrls[0], m, ct));
                var processed = await Task.WhenAll(tasks);
                return processed.Where(m => m != null).ToList()!;
            }
            finally
            {
                try { await page.CloseAsync(); } catch { }
            }
        }

        public Task<List<HanimeMetadata>> SearchByFilenameAsync(
            string filenameOrText,
            int maxResults = 12,
            string? sort = "最新上市")
            => SearchByFilenameAsync(filenameOrText, maxResults, sort, CancellationToken.None);

        // ===================== 直达详情抓取 =====================
        private async Task<List<HanimeMetadata>> DirectFetchAsync(string detailUrl, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!detailUrl.StartsWith($"{Host}/watch", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("只允许 hanime1.me 域名的直达 URL。");

            var meta = new HanimeMetadata();
            meta.SourceUrls.Add(detailUrl);
            TryExtractId(detailUrl, out var id);
            meta.ID = id;

            var processed = await ProcessDetailAsync(detailUrl, meta, ct);
            if (processed is null) return new List<HanimeMetadata>();

            // 用详情页信息覆盖
            if (!string.IsNullOrWhiteSpace(processed.OriginalTitle))
                processed.Title = processed.OriginalTitle;

            var thumb = processed.Thumbnails?.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            if (!string.IsNullOrWhiteSpace(thumb))
                processed.Primary = thumb;

            return new List<HanimeMetadata> { processed };
        }

        // ===================== 单个详情（带并发限流 + 慢速重试） =====================
        private async Task<HanimeMetadata?> ProcessDetailAsync(string detailUrl, HanimeMetadata meta, CancellationToken ct)
        {
            await _detailConcurrency.WaitAsync(ct);
            IPage? page = null;

            try
            {
                // —— 第一次尝试：在“详情用”Context 里 —— 
                var ctx = await _ctxMgr.GetOrCreateContextAsync(forDetail: true);
                page = await ctx.NewPageAsync();
                _ctxMgr.BumpOpenedPages(ctx, forDetail: true);

                try
                {
                    await TryGotoAndWaitAsync(page, detailUrl, timeoutMs: 60000, ct);
                    await DoAntiBotActionsAsync(page, ct);

                    // 先用 Locator 取关键字段；失败回退到 HTML 解析
                    await TryFillMetaViaLocatorAsync(page, meta);
                    var html = await page.ContentAsync();

                    if (LooksLikeChallenge(page.Url, html))
                        throw new InvalidOperationException("Challenge detected on primary attempt.");

                    // 回退完整解析，弥补 Locator 没覆盖的字段
                    ParseHanimeDetail(html, meta);

                    return meta;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex1)
                {
                    _logger.LogWarning(ex1, "Primary attempt failed, slow-retry with NEW temp context: {Url}", detailUrl);

                    // —— 第二次尝试：使用全新临时 Context 慢速重试 —— 
                    await SafeClosePage(page);
                    page = null;

                    var tempCtx = await NewTempContextAsync();
                    try
                    {
                        var tmpPage = await tempCtx.NewPageAsync();
                        page = tmpPage;

                        await TryGotoAndWaitAsync(tmpPage, detailUrl, timeoutMs: _ctxMgr.Options.SlowRetryGotoTimeoutMs, ct);
                        await DoAntiBotActionsAsync(tmpPage, ct);

                        await TryFillMetaViaLocatorAsync(tmpPage, meta);
                        var html2 = await tmpPage.ContentAsync();

                        if (LooksLikeChallenge(tmpPage.Url, html2))
                            throw new InvalidOperationException("Challenge detected on slow-retry.");

                        ParseHanimeDetail(html2, meta);

                        // 慢速重试成功，标记当前“详情用”Context 以便轮换
                        _ctxMgr.FlagChallengeOnCurrent(forDetail: true);
                        _logger.LogInformation("Slow-retry succeeded; flag detail context to rotate.");

                        return meta;
                    }
                    finally
                    {
                        try { await tempCtx.CloseAsync(); } catch { }
                    }
                }
            }
            finally
            {
                await SafeClosePage(page);
                _detailConcurrency.Release();
            }
        }

        private bool LooksLikeChallenge(string? url, string? html)
        {
            var opt = _ctxMgr.Options;
            if (!string.IsNullOrEmpty(url) && opt.ChallengeUrlHints.Any(h => url.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            if (!string.IsNullOrEmpty(html) && opt.ChallengeDomHints.Any(h => html.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            return false;
        }

        private async Task TryGotoAndWaitAsync(IPage page, string url, int timeoutMs, CancellationToken ct)
        {
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeoutMs });
            // 等待关键元素更稳
            await page.Locator("#shareBtn-title").First.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = _ctxMgr.Options.SlowRetryWaitSelectorMs
            });
        }

        private static async Task SafeClosePage(IPage? p)
        {
            if (p is null) return;
            try { if (!p.IsClosed) await p.CloseAsync(); } catch { }
        }

        private async Task<IBrowserContext> NewTempContextAsync()
        {
            // 使用 ContextManager 的配置创建一个“一次性”的隔离 Context（不纳入统计）
            var browser = (_ctxMgr.GetType()
                .GetField("_browser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(_ctxMgr)) as IBrowser;

            if (browser == null)
                throw new InvalidOperationException("Cannot access browser from context manager.");

            // 复制配置
            var opt = _ctxMgr.Options;
            var ctx = await browser.NewContextAsync(new()
            {
                UserAgent = opt.UserAgent,
                ViewportSize = new() { Width = opt.ViewportWidth, Height = opt.ViewportHeight },
                Locale = opt.Locale,
                TimezoneId = opt.TimezoneId
            });
            await ctx.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { { "Accept-Language", opt.AcceptLanguage } });

            try
            {
                if (!string.IsNullOrWhiteSpace(opt.StealthInitRelativePath))
                {
                    var path = Path.IsPathRooted(opt.StealthInitRelativePath)
                        ? opt.StealthInitRelativePath
                        : Path.Combine(AppContext.BaseDirectory, opt.StealthInitRelativePath);

                    if (File.Exists(path))
                    {
                        var script = await File.ReadAllTextAsync(path);
                        await ctx.AddInitScriptAsync(script);
                    }
                }
            }
            catch { /* ignore */ }

            return ctx;
        }

        // ===== 反爬动作：更“真人化”的随机移动/悬停/滚动/键盘（但不点击） =====
        private async Task DoAntiBotActionsAsync(IPage page, CancellationToken ct)
        {
            try
            {
                static async Task WaitRandomAsync(Random rnd, int minMs, int maxMs, CancellationToken token)
                {
                    var delay = rnd.Next(minMs, maxMs);
                    await Task.Delay(delay, token);
                }

                static async Task MoveMouseHumanAsync(IMouse mouse, int fromX, int fromY, int toX, int toY, int steps, Random rnd, CancellationToken token)
                {
                    var dx = (toX - fromX) / (float)steps;
                    var dy = (toY - fromY) / (float)steps;
                    for (int i = 1; i <= steps; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var x = fromX + dx * i;
                        var y = fromY + dy * i;
                        await mouse.MoveAsync(x, y, new MouseMoveOptions { Steps = 1 });
                        await Task.Delay(rnd.Next(15, 70), token);
                    }
                }

                await WaitRandomAsync(_rnd, 600, 1400, ct);

                int viewportWidth = 1280, viewportHeight = 900;
                try
                {
                    var vp = page.ViewportSize;
                    if (vp != null) { viewportWidth = vp.Width; viewportHeight = vp.Height; }
                }
                catch { }

                var startX = _rnd.Next(60, Math.Max(100, Math.Min(600, viewportWidth / 2)));
                var startY = _rnd.Next(60, Math.Max(100, Math.Min(500, viewportHeight / 2)));
                var midX = _rnd.Next(viewportWidth / 4, viewportWidth * 3 / 4);
                var midY = _rnd.Next(viewportHeight / 4, viewportHeight * 3 / 4);

                await MoveMouseHumanAsync(page.Mouse, startX, startY, midX, midY, _rnd.Next(6, 16), _rnd, ct);

                try
                {
                    var candidates = await page.QuerySelectorAllAsync("a, img, button, div[role='button']");
                    if (candidates != null && candidates.Count > 0)
                    {
                        int toHover = _rnd.Next(0, 3);
                        for (int i = 0; i < toHover; i++)
                        {
                            ct.ThrowIfCancellationRequested();
                            var node = candidates[_rnd.Next(candidates.Count)];
                            try
                            {
                                var box = await node.BoundingBoxAsync();
                                if (box != null)
                                {
                                    var hx = (int)(box.X + box.Width / 2);
                                    var hy = (int)(box.Y + box.Height / 2);
                                    await MoveMouseHumanAsync(page.Mouse, midX, midY, hx, hy, _rnd.Next(6, 12), _rnd, ct);
                                    await node.HoverAsync();
                                    await WaitRandomAsync(_rnd, 500, 1400, ct);
                                    midX = hx; midY = hy;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                if (_rnd.NextDouble() < 0.9)
                {
                    int totalScroll = _rnd.Next(150, 1000);
                    int chunk = _rnd.Next(80, 220);
                    int scrolled = 0;
                    while (scrolled < totalScroll)
                    {
                        ct.ThrowIfCancellationRequested();
                        var delta = Math.Min(chunk, totalScroll - scrolled);
                        await page.Mouse.WheelAsync(0, delta);
                        scrolled += delta;
                        await Task.Delay(_rnd.Next(180, 450), ct);
                    }
                }

                if (_rnd.NextDouble() < 0.35)
                {
                    ct.ThrowIfCancellationRequested();
                    if (_rnd.NextDouble() < 0.5)
                        await page.Keyboard.PressAsync("PageDown");
                    else
                        await page.Keyboard.PressAsync("ArrowDown");

                    await Task.Delay(_rnd.Next(200, 700), ct);
                }

                var endX = _rnd.Next(20, Math.Min(200, viewportWidth - 20));
                var endY = _rnd.Next(20, Math.Min(200, viewportHeight - 20));
                await MoveMouseHumanAsync(page.Mouse, midX, midY, endX, endY, _rnd.Next(8, 18), _rnd, ct);

                await WaitRandomAsync(_rnd, 700, 1600, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* ignore */ }
        }

        // ============ Locator 优先填充，失败回退 HtmlAgilityPack ============
        private async Task TryFillMetaViaLocatorAsync(IPage page, HanimeMetadata meta)
        {
            try
            {
                var titleNode = page.Locator("#shareBtn-title");
                if (await titleNode.CountAsync() > 0)
                {
                    var rawTitle = (await titleNode.InnerTextAsync()).Trim();
                    var re = new Regex(@"\[[^\]]*\]");
                    var cleanTitle = re.Replace(rawTitle, string.Empty, 1 /*count*/, 0 /*startat*/).Trim();
                    // 如果你的 .NET 版本支持 3 参重载，也可以：re.Replace(rawTitle, string.Empty, 1)
                    meta.OriginalTitle = cleanTitle;
                    if (string.IsNullOrWhiteSpace(meta.Title)) meta.Title = cleanTitle;
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
            catch
            {
                // 忽略，回退到 ParseHanimeDetail
            }
        }

        private void ParseHanimeDetail(string html, HanimeMetadata meta)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // OriginalTitle
            var titleNode = doc.DocumentNode.SelectSingleNode("//h3[@id='shareBtn-title']");
            if (titleNode != null)
            {
                var rawTitle = titleNode.InnerText.Trim();
                var regex = new Regex(@"\[.*?\]");
                var cleanTitle = regex.Replace(rawTitle, "", 1).Trim();
                meta.OriginalTitle = cleanTitle;
                if (string.IsNullOrWhiteSpace(meta.Title)) meta.Title = cleanTitle;
            }

            // 描述
            var capDiv = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'video-caption-text')]");
            if (capDiv != null) meta.Description = capDiv.InnerText.Trim();

            // 标签
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tagDivs = doc.DocumentNode.SelectNodes("//div[@class='single-video-tag' and not(@data-toggle) and not(@data-target)]");
            if (tagDivs != null)
            {
                foreach (var div in tagDivs)
                {
                    var a = div.SelectSingleNode("./a");
                    string tag = "";
                    if (a != null)
                    {
                        var textNode = a.SelectSingleNode("text()");
                        tag = (textNode?.InnerText ?? a.InnerText) ?? "";
                    }
                    else
                    {
                        tag = div.InnerText ?? "";
                    }
                    tag = CleanTag(tag);
                    if (!string.IsNullOrWhiteSpace(tag)) tags.Add(tag);
                }
            }
            meta.Genres = tags.ToList();

            // 评分（百分比转 0-5）
            var likeDiv = doc.DocumentNode.SelectSingleNode("//div[@id='video-like-form-wrapper']//div[contains(@class,'single-icon')]");
            if (likeDiv != null)
            {
                string text = string.Concat(
                    likeDiv.ChildNodes
                        .Where(n => n.NodeType == HtmlNodeType.Text)
                        .Select(n => n.InnerText)
                ).Trim();

                var match = Regex.Match(text, @"(\d+)%");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int percent))
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
                var text = descDiv.InnerText;
                var match = Regex.Match(text, @"\d{4}-\d{2}-\d{2}");
                if (match.Success && DateTime.TryParse(match.Value, out DateTime date))
                {
                    meta.ReleaseDate = date;
                    meta.Year = date.Year;
                }
            }

            // 海报
            var posterNode = doc.DocumentNode.SelectSingleNode("//video[@poster]");
            if (posterNode != null)
            {
                var posterUrl = posterNode.GetAttributeValue("poster", "");
                if (!string.IsNullOrWhiteSpace(posterUrl) && !meta.Thumbnails.Contains(posterUrl))
                    meta.Thumbnails.Add(posterUrl);
                if (string.IsNullOrWhiteSpace(meta.Primary)) meta.Primary = posterUrl;
            }
        }

        // ===================== 工具/解析辅助 =====================
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

        private static string BuildQueryFromFilename(string filenameOrText)
        {
            if (string.IsNullOrWhiteSpace(filenameOrText)) return "";
            var name = Path.GetFileNameWithoutExtension(filenameOrText.Trim());

            var cleaned = Regex.Replace(name, @"(?i)\b(1080p|2160p|720p|480p|hevc|x265|x264|h\.?264|h\.?265|aac|flac|hdr|dv|10bit|8bit|webrip|web-dl|bluray|remux|sub|chs|cht|eng|multi|unrated|proper|repack)\b", " ");
            cleaned = Regex.Replace(cleaned, @"[\[\]\(\)\{\}【】（）]", " ");
            cleaned = Regex.Replace(cleaned, @"[_\.]+", " ");
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();

            if (string.IsNullOrWhiteSpace(cleaned))
                cleaned = name;

            return cleaned;
        }

        private static readonly Regex HanimeUrlNumericIdRegex = new(@"(?i)https?://(?:www\.)?hanime1\.me/watch\?v=(\d{3,})", RegexOptions.Compiled);
        private static readonly Regex HanimeBareNumericIdRegex = new(@"^\d{3,}$", RegexOptions.Compiled);

        private static bool TryParseHanimeNumericId(string? text, out string id)
        {
            id = string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var t = text.Trim();

            var m = HanimeUrlNumericIdRegex.Match(t);
            if (m.Success) { id = m.Groups[1].Value; return true; }

            if (HanimeBareNumericIdRegex.IsMatch(t)) { id = t; return true; }

            return false;
        }

        private static string BuildDetailUrlById(string numericId) => $"{Host}/watch?v={numericId}";

        private static bool TryExtractId(string url, out string id)
        {
            id = "";
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u)) return false;
            if (!u.Host.Equals("hanime1.me", StringComparison.OrdinalIgnoreCase)
             && !u.Host.Equals("www.hanime1.me", StringComparison.OrdinalIgnoreCase)) return false;

            var m = Regex.Match(u.Query, @"(?:^|[?&])v=(\d{3,})\b");
            if (!m.Success) return false;

            id = m.Groups[1].Value;
            return true;
        }
    }
}
