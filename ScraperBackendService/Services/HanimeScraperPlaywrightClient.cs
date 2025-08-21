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
using ScraperBackendService.Models;

namespace ScraperBackendService.Services
{
    /// <summary>
    /// 抓取分流标志位：
    /// - ById：按“数字ID”直达（支持纯数字或 https://hanime1.me/watch?v=12345）
    /// - ByFilename：按文件名/标题搜索
    /// - Auto：先当ID，失败则按文件名（可选）
    /// </summary>
    public enum HanimeScrapeRoute
    {
        Auto = 0,
        ById = 1,
        ByFilename = 2
    }

    public class HanimeScraperPlaywrightClient
    {
        private readonly IBrowser _browser;
        private readonly ILogger<HanimeScraperPlaywrightClient> _logger;

        // ===== 复用的 BrowserContext 与并发控制 =====
        private IBrowserContext? _context;
        private readonly SemaphoreSlim _detailConcurrency; // 控制同时打开的详情页数量
        private readonly Random _rnd = new();

        // 可调：详情页最大并发（建议 2~4，更像真人）
        private const int MAX_DETAIL_CONCURRENCY = 3;
        private const string Host = "https://hanime1.me";

        public HanimeScraperPlaywrightClient(IBrowser browser, ILogger<HanimeScraperPlaywrightClient> logger)
        {
            _browser = browser;
            _logger = logger;
            _detailConcurrency = new SemaphoreSlim(MAX_DETAIL_CONCURRENCY);
        }

        // ===================== 对外主入口（带标志位分流） =====================

        /// <summary>
        /// 统一对外：前端传入“分流标志位”，在此进行分流。
        /// </summary>
        public async Task<List<HanimeMetadata>> FetchAsync(string input, HanimeScrapeRoute route, int maxResults = 12, string? sort = "最新上市")
        {
            switch (route)
            {
                case HanimeScrapeRoute.ById:
                    {
                        if (!TryParseHanimeNumericId(input, out var id))
                            throw new ArgumentException($"按ID模式解析失败，输入不是合法的 Hanime 数字ID（支持：纯数字 或 {Host}/watch?v=12345）。输入：{input}");
                        _logger.LogInformation("Hanime 按ID直达: {Id}", id);
                        return await FetchByIdAsync(id);
                    }

                case HanimeScrapeRoute.ByFilename:
                    {
                        _logger.LogInformation("Hanime 按文件名搜索: {Input}", input);
                        return await SearchByFilenameAsync(input, maxResults, sort);
                    }

                case HanimeScrapeRoute.Auto:
                default:
                    {
                        if (TryParseHanimeNumericId(input, out var id))
                        {
                            _logger.LogInformation("Hanime Auto→按ID直达: {Id}", id);
                            return await FetchByIdAsync(id);
                        }
                        _logger.LogInformation("Hanime Auto→按文件名搜索: {Input}", input);
                        return await SearchByFilenameAsync(input, maxResults, sort);
                    }
            }
        }

        /// <summary>
        /// 为兼容旧签名保留（内部等价于 Auto 分流）。
        /// </summary>
        public Task<List<HanimeMetadata>> SmartSearchAndFetchAllAsync(string searchTitle, int maxResults = 12, string? sort = "最新上市")
            => FetchAsync(searchTitle, HanimeScrapeRoute.Auto, maxResults, sort);

        /// <summary>
        /// 按“数字ID”直达抓取（仅处理数字ID，不处理 slug）
        /// </summary>
        public async Task<List<HanimeMetadata>> FetchByIdAsync(string numericId)
        {
            if (string.IsNullOrWhiteSpace(numericId) || !HanimeBareNumericIdRegex.IsMatch(numericId))
                throw new ArgumentException("Hanime ID 非法（需为纯数字字符串，长度≥3）。", nameof(numericId));

            var url = BuildDetailUrlById(numericId);
            _logger.LogInformation("Hanime 直达URL: {Url}", url);
            return await DirectFetchAsync(url);
        }

        /// <summary>
        /// 文件名搜索：清洗文件名 -> 关键词搜索 -> 抓详情
        /// </summary>
        public async Task<List<HanimeMetadata>> SearchByFilenameAsync(string filenameOrText, int maxResults = 12, string? sort = "最新上市")
        {
            string keyword = BuildQueryFromFilename(filenameOrText);
            if (string.IsNullOrWhiteSpace(keyword))
                keyword = filenameOrText?.Trim() ?? string.Empty;

            string sortParam = string.IsNullOrWhiteSpace(sort) ? "" : $"&sort={Uri.EscapeDataString(sort)}";
            string queryParam = $"?query={Uri.EscapeDataString(keyword)}";
            string url = $"{Host}/search{queryParam}{sortParam}";

            var context = await GetOrCreateContextAsync();
            var page = await context.NewPageAsync(); // 仅开一个搜索页页面

            _logger.LogInformation("Hanime 搜索: {Url}", url);
            var results = await SearchOnceAsync(page, url, maxResults);

            await page.CloseAsync(); // 关闭搜索页（详情页是单独开的）
            // 不关闭 context：给下一次关键词复用

            return results;
        }

        // ===================== 数字ID解析 & URL构建 =====================

        // 仅支持两种输入：纯数字 或 https://hanime1.me/watch?v=12345
        private static readonly Regex HanimeUrlNumericIdRegex = new(@"(?i)https?://(?:www\.)?hanime1\.me/watch\?v=(\d{3,})", RegexOptions.Compiled);
        private static readonly Regex HanimeBareNumericIdRegex = new(@"^\d{3,}$", RegexOptions.Compiled);

        private static bool TryParseHanimeNumericId(string? text, out string id)
        {
            id = string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var t = text.Trim();

            // URL 形式：.../watch?v=12345
            var m = HanimeUrlNumericIdRegex.Match(t);
            if (m.Success) { id = m.Groups[1].Value; return true; }

            // 纯数字
            if (HanimeBareNumericIdRegex.IsMatch(t)) { id = t; return true; }

            return false;
        }

        private static string BuildDetailUrlById(string numericId) => $"{Host}/watch?v={numericId}";

        // ===================== Context 复用 =====================

        // 复用 Context（只创建一次，后续直接使用；挂了会重建）
        private async Task<IBrowserContext> GetOrCreateContextAsync()
        {
            // Browser 还连着吗？
            bool browserAlive = _context?.Browser?.IsConnected ?? false;

            if (_context == null || !browserAlive)
            {
                // 旧的尽量关一下（忽略失败）
                if (_context != null)
                {
                    try { await _context.CloseAsync(); } catch { /* ignore */ }
                }

                _context = await _browser.NewContextAsync(new()
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
                    Locale = "en-US",
                    TimezoneId = "Asia/Shanghai"
                });

                await _context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
                {
                    { "Accept-Language", "en-US,en;q=0.9" }
                });

                // 如需 stealth，这里注入
                // var stealthPath = Path.Combine(AppContext.BaseDirectory, "StealthInit.js");
                // if (File.Exists(stealthPath))
                //     await _context.AddInitScriptAsync(await File.ReadAllTextAsync(stealthPath));
            }

            return _context;
        }

        // ===================== 搜索页收集与详情抓取 =====================

        private async Task<List<HanimeMetadata>> SearchOnceAsync(IPage page, string url, int maxResults)
        {
            var results = new ConcurrentBag<HanimeMetadata>();

            // ① 打开搜索页
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await DoAntiBotActionsAsync(page); // 反爬动作（滚动/停顿）

            var html = await page.ContentAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var videoDivs = doc.DocumentNode.SelectNodes("//div[@title]");
            if (videoDivs == null || videoDivs.Count == 0)
                return results.ToList();

            var detailUrlSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var metaMap = new Dictionary<string, HanimeMetadata>(StringComparer.OrdinalIgnoreCase);

            // ② 收集搜索结果（仅保留 https://hanime1.me/watch...）
            var baseUri = new Uri(Host);

            foreach (var div in videoDivs)
            {
                var link = div.SelectSingleNode(".//a[@class='overlay']");
                var hrefRaw = link?.GetAttributeValue("href", null)?.Trim();
                if (string.IsNullOrWhiteSpace(hrefRaw)) continue;

                // 统一规范为绝对 URL（相对/协议相对都能补全）
                if (!Uri.TryCreate(baseUri, hrefRaw, out var absUri)) continue;
                var absUrl = absUri.AbsoluteUri;

                // 只保留目标域 + watch 路径（最直接、最稳）
                if (!absUrl.StartsWith($"{Host}/watch", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("过滤非目标链接: {Url}", absUrl);
                    continue;
                }

                // 去重使用“规范化后”的 URL
                if (!detailUrlSet.Add(absUrl)) continue;

                var meta = new HanimeMetadata
                {
                    Title = div.GetAttributeValue("title", null)?.Trim()
                };
                meta.SourceUrls.Add(absUrl);
                TryExtractId(absUrl, out var id);
                meta.ID = id; // 直接从 URL 提取 ID

                var img = div.SelectSingleNode(".//img[contains(@src,'/thumbnail/')]");
                if (img != null)
                {
                    var coverUrl = img.GetAttributeValue("src", null);
                    if (!string.IsNullOrWhiteSpace(coverUrl))
                        meta.Primary = coverUrl;
                }

                metaMap[absUrl] = meta;                // ✅ 用规范化后的 URL 做 key
                if (metaMap.Count >= maxResults) break;
            }

            if (metaMap.Count == 0) return results.ToList();

            // ③ 并发访问详情页（使用与搜索页相同的 Context，保持 cookie/session）
            var context = page.Context;
            var tasks = metaMap.Select(kv => ProcessDetailAsync(context, kv.Key, kv.Value));

            // 限制并发并汇总
            var processed = await Task.WhenAll(tasks);
            foreach (var meta in processed)
            {
                if (meta != null) results.Add(meta);
            }

            return results.ToList();
        }

        /// <summary>
        /// 直达详情抓取（从 URL 直接开页解析），返回单条结果列表以保持统一签名
        /// </summary>
        private async Task<List<HanimeMetadata>> DirectFetchAsync(string detailUrl)
        {
            // 仅白名单域名
            if (!detailUrl.StartsWith($"{Host}/watch", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("只允许 hanime1.me 域名的直达 URL。");

            var context = await GetOrCreateContextAsync();

            var meta = new HanimeMetadata();
            meta.SourceUrls.Add(detailUrl);
            TryExtractId(detailUrl, out var id);
            meta.ID = id; // 直接从 URL 提取 ID

            var processed = await ProcessDetailAsync(context, detailUrl, meta);
            if (processed is null) return new List<HanimeMetadata>();

            // === 关键：用详情页信息覆盖 ===
            // 1) original title 覆盖 title
            if (!string.IsNullOrWhiteSpace(processed.OriginalTitle))
                processed.Title = processed.OriginalTitle;

            // 2) 用缩略图（thumb/poster）覆盖 cover（Primary）
            //    ParseHanimeDetail 会把 <video poster="..."> 放到 Thumbnails，并在 Primary 为空时赋值。
            //    为确保覆盖，这里无论 Primary 先前是否有值，都用第一个缩略图替换。
            var thumb = processed.Thumbnails?.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            if (!string.IsNullOrWhiteSpace(thumb))
                processed.Primary = thumb;

            return new List<HanimeMetadata> { processed };
        }

        // 单个详情页的处理（受并发池保护）
        private async Task<HanimeMetadata?> ProcessDetailAsync(IBrowserContext context, string detailUrl, HanimeMetadata meta)
        {
            await _detailConcurrency.WaitAsync();
            IPage? detailPage = null;

            try
            {
                detailPage = await context.NewPageAsync();

                await detailPage.GotoAsync(detailUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });

                // 可选：等待网络空闲一会，给动态脚本完成时间
                // await detailPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // 反爬动作
                await DoAntiBotActionsAsync(detailPage);

                var detailHtml = await detailPage.ContentAsync();
                ParseHanimeDetail(detailHtml, meta);

                return meta;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "访问详情页失败: {Url}", detailUrl);
                return null;
            }
            finally
            {
                if (detailPage != null && !detailPage.IsClosed)
                    await detailPage.CloseAsync();

                _detailConcurrency.Release();
            }
        }

        // ===== 反爬动作：随机停顿 + 滚动 + 鼠标/键盘轻触 =====
        private async Task DoAntiBotActionsAsync(IPage page)
        {
            try
            {
                // 800~1600ms 的随机停顿
                await page.WaitForTimeoutAsync(_rnd.Next(800, 1600));

                // 随机移动鼠标到页面内某区域
                await page.Mouse.MoveAsync(_rnd.Next(60, 600), _rnd.Next(60, 500));

                // 30% 概率轻点一下
                if (_rnd.NextDouble() < 0.3)
                    await page.Mouse.ClickAsync(_rnd.Next(80, 400), _rnd.Next(80, 300));

                // 60% 概率滚动一段距离
                if (_rnd.NextDouble() < 0.6)
                    await page.Mouse.WheelAsync(0, _rnd.Next(200, 800));

                // 偶尔按一下方向键
                if (_rnd.NextDouble() < 0.25)
                    await page.Keyboard.PressAsync("ArrowDown");
            }
            catch
            {
                // 反爬动作失败不影响主流程
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

                if (string.IsNullOrWhiteSpace(meta.Title))
                    meta.Title = cleanTitle;
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

        // ===================== 文件名 -> 关键词 清洗 =====================

        private static string BuildQueryFromFilename(string filenameOrText)
        {
            if (string.IsNullOrWhiteSpace(filenameOrText)) return "";

            // 1) 取文件名（去路径、去扩展名）
            var name = Path.GetFileNameWithoutExtension(filenameOrText.Trim());

            // 2) 去掉常见无关标记（分辨率、编码、字幕等）
            var cleaned = Regex.Replace(name, @"(?i)\b(1080p|2160p|720p|480p|hevc|x265|x264|h\.?264|h\.?265|aac|flac|hdr|dv|10bit|8bit|webrip|web-dl|bluray|remux|sub|chs|cht|eng|multi|unrated|proper|repack)\b", " ");
            cleaned = Regex.Replace(cleaned, @"[\[\]\(\)\{\}【】（）]", " "); // 括号替空格
            cleaned = Regex.Replace(cleaned, @"[_\.]+", " ");                // 下划线/点转空格
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();

            // 3) 如果清洗过度，至少返回原始名
            if (string.IsNullOrWhiteSpace(cleaned))
                cleaned = name;

            return cleaned;
        }

        // 从 https://hanime1.me/watch?v=86994[&...] 提取 86994
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
