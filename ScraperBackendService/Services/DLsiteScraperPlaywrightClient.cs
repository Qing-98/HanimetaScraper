using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ScraperBackendService.Models;

namespace ScraperBackendService.Services
{
    /// <summary>
    /// 分流标志位（与 Hanime 语义一致）：
    /// - ById：RJ/VJ 编号或 DLsite 详情 URL
    /// - ByFilename：按文件名/标题搜索
    /// - Auto：先按 ID，失败则按标题搜索
    /// </summary>
    public enum DLsiteScrapeRoute { Auto = 0, ById = 1, ByFilename = 2 }

    /// <summary>
    /// 纯 HttpClient + HtmlAgilityPack 的 DLsite 刮削客户端（轻量）
    /// 输出模型：HanimeMetadata / PersonDto（与项目现有一致）
    /// </summary>
    public sealed class DLsiteScraperHttpClient
    {
        private readonly ILogger<DLsiteScraperHttpClient> _logger;

        public DLsiteScraperHttpClient(ILogger<DLsiteScraperHttpClient> logger)
        {
            _logger = logger;
        }

        // ===== 常量 / 路径 =====
        private const string ProviderName = "DLsite";
        private const string ManiaxWorkUrl = "https://www.dlsite.com/maniax/work/=/product_id/{0}.html";
        private const string ProWorkUrl = "https://www.dlsite.com/pro/work/=/product_id/{0}.html";
        private const string UnifiedSearchUrl = "https://www.dlsite.com/maniax/fsr/=/keyword/{0}/work_type_category[0]/movie/";

        // ===== 共享 HttpClient（轻量池化）=====
        private static readonly SocketsHttpHandler Handler = new()
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(90),
            MaxConnectionsPerServer = 12,
            EnableMultipleHttp2Connections = true,
            UseCookies = false,
            Proxy = WebRequest.DefaultWebProxy,
        };
        private static readonly HttpClient Http = new(Handler) { Timeout = TimeSpan.FromSeconds(30) };
        static DLsiteScraperHttpClient()
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118 Safari/537.36");
            Http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ja,en;q=0.9,zh-CN;q=0.8");
        }

        // ===== 正则（最小集合）=====
        private static readonly Regex RjRe = new(@"(?i)^RJ\d+$", RegexOptions.Compiled);
        private static readonly Regex VjRe = new(@"(?i)^VJ\d+$", RegexOptions.Compiled);
        private static readonly Regex ProductPathRe = new(@"(?i)^(RJ|VJ)\d+\.html$", RegexOptions.Compiled);
        private static readonly Regex SpaceCollapseRe = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex DateJpRe = new(@"(\d{4})年(\d{1,2})月(\d{1,2})日", RegexOptions.Compiled);

        private const int MAX_DETAIL_CONCURRENCY = 5;

        // ================= 对外主入口（与 Hanime 一致） =================

        public Task<List<HanimeMetadata>> SmartSearchAndFetchAllAsync(string searchTitle, int maxResults = 12, CancellationToken ct = default)
            => FetchAsync(searchTitle, DLsiteScrapeRoute.Auto, maxResults, ct);

        public async Task<List<HanimeMetadata>> FetchAsync(string input, DLsiteScrapeRoute route, int maxResults = 12, CancellationToken ct = default)
        {
            switch (route)
            {
                case DLsiteScrapeRoute.ById:
                    if (!TryParseDlsiteId(input, out var id))
                        throw new ArgumentException($"按ID模式解析失败（需要 RJ/VJ 或 DLsite 详情URL）：{input}");
                    _logger.LogInformation("DLsite 按ID直达: {Id}", id);
                    return await FetchByIdAsync(id, ct);

                case DLsiteScrapeRoute.ByFilename:
                    _logger.LogInformation("DLsite 按标题搜索: {Q}", input);
                    return await SearchByFilenameAsync(input, maxResults, ct);

                case DLsiteScrapeRoute.Auto:
                default:
                    if (TryParseDlsiteId(input, out id))
                    {
                        _logger.LogInformation("DLsite Auto→按ID直达: {Id}", id);
                        return await FetchByIdAsync(id, ct);
                    }
                    _logger.LogInformation("DLsite Auto→按标题搜索: {Q}", input);
                    return await SearchByFilenameAsync(input, maxResults, ct);
            }
        }

        public async Task<List<HanimeMetadata>> FetchByIdAsync(string id, CancellationToken ct = default)
        {
            id = id.Trim().ToUpperInvariant();
            var meta = await GetMovieInfoByIDAsync(id, ct);
            return meta is null ? new() : new() { meta };
        }

        public async Task<List<HanimeMetadata>> SearchByFilenameAsync(string filenameOrText, int maxResults = 12, CancellationToken ct = default)
        {
            var kw = NormalizeKeyword(BuildQueryFromFilename(filenameOrText));
            if (string.IsNullOrWhiteSpace(kw)) kw = NormalizeKeyword(filenameOrText ?? "");
            var ids = await CollectIdsFromSearchAsync(kw, ct);
            if (ids.Count == 0) return new();

            if (maxResults > 0 && ids.Count > maxResults) ids = ids.Take(maxResults).ToList();

            var bag = new ConcurrentBag<HanimeMetadata>();
            using var sem = new SemaphoreSlim(MAX_DETAIL_CONCURRENCY);
            var tasks = ids.Select(async id =>
            {
                await sem.WaitAsync(ct);
                try
                {
                    var info = await GetMovieInfoByIDAsync(id, ct);
                    if (info != null) bag.Add(info);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DLsite 详情抓取失败: {Id}", id);
                }
                finally { sem.Release(); }
            });
            await Task.WhenAll(tasks);

            // 按搜索顺序还原
            var map = bag.ToDictionary(m => m.ID ?? "", m => m, StringComparer.OrdinalIgnoreCase);
            var ordered = new List<HanimeMetadata>();
            foreach (var id in ids) if (map.TryGetValue(id, out var m)) ordered.Add(m);
            return ordered;
        }

        // ================= 详情抓取 =================

        public async Task<HanimeMetadata?> GetMovieInfoByIDAsync(string id, CancellationToken ct = default)
        {
            id = id.Trim().ToUpperInvariant();

            if (RjRe.IsMatch(id))
            {
                var a = await GetMovieInfoByURLAsync(string.Format(CultureInfo.InvariantCulture, ManiaxWorkUrl, id), ct);
                if (a != null) return a;
                return await GetMovieInfoByURLAsync(string.Format(CultureInfo.InvariantCulture, ProWorkUrl, id), ct);
            }
            if (VjRe.IsMatch(id))
            {
                var a = await GetMovieInfoByURLAsync(string.Format(CultureInfo.InvariantCulture, ProWorkUrl, id), ct);
                if (a != null) return a;
                return await GetMovieInfoByURLAsync(string.Format(CultureInfo.InvariantCulture, ManiaxWorkUrl, id), ct);
            }

            // 兜底
            var b = await GetMovieInfoByURLAsync(string.Format(CultureInfo.InvariantCulture, ManiaxWorkUrl, id), ct);
            if (b != null) return b;
            return await GetMovieInfoByURLAsync(string.Format(CultureInfo.InvariantCulture, ProWorkUrl, id), ct);
        }

        public async Task<HanimeMetadata?> GetMovieInfoByURLAsync(string url, CancellationToken ct = default)
        {
            var id = ParseMovieIDFromURL(url);
            if (string.IsNullOrEmpty(id)) return null;

            var html = await GetStringAsync(url, ct);
            if (string.IsNullOrWhiteSpace(html)) return null;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var meta = new HanimeMetadata
            {
                ID = id,
                SourceUrls = new List<string> { url }
            };

            // 标题
            meta.Title = Clean(SelectText(doc, "//h1[@id='work_name']"));

            // 简介 → Description
            meta.Description = ExtractSummary(doc);

            // 厂牌 maker ⇒ Studios
            var maker = ExtractOutlineCell(doc, "//table[@id='work_maker']//tr[.//th[contains(normalize-space(.),'ブランド名') or contains(normalize-space(.),'サークル名')]]//td");
            if (!string.IsNullOrWhiteSpace(maker)) meta.Studios.Add(maker);

            // 系列 ⇒ Series 列表
            var series = ExtractOutlineCell(doc, "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'シリーズ')]]//td");
            if (!string.IsNullOrWhiteSpace(series)) meta.Series.Add(series);

            // Genres
            foreach (var a in SelectNodes(doc, "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'ジャンル')]]//td//div[contains(@class,'main_genre')]//a"))
            {
                var g = Clean(a.InnerText);
                if (!string.IsNullOrEmpty(g) && !meta.Genres.Contains(g)) meta.Genres.Add(g);
            }

            // 发布日期
            var rawDate = ExtractOutlineCellPreferA(doc, "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'販売日')]]//td");
            var m = DateJpRe.Match(rawDate ?? "");
            if (m.Success)
            {
                var y = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var M = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                var d = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                var dt = new DateTimeOffset(new DateTime(y, M, d, 0, 0, 0, DateTimeKind.Utc));
                meta.ReleaseDate = dt;
                meta.Year = dt.Year;
            }

            // 人员：演员/导演/原画/脚本/制作等
            ExtractPeople(doc, meta);

            // 图片：大封面→Primary；大缩略→Backdrop；其它全部→Thumbnails
            // 大图（active slider）
            var bigImg = SelectSingle(doc, "//*[@id='work_left']//div[contains(@class,'work_slider_container')]//li[contains(@class,'slider_item') and contains(@class,'active')]//img");
            var bigPick = PickJpg(GetAttr(bigImg, "src"), GetAttr(bigImg, "srcset"), url);
            if (!string.IsNullOrEmpty(bigPick))
            {
                meta.Primary = bigPick;          // BigCoverUrl ⇒ Primary
                meta.Backdrop = bigPick;         // 如需严格区分，可在下方覆盖 Backdrop
            }

            // 缩略（第一个 data-thumb 作为大缩略）
            var firstThumbDiv = SelectSingle(doc, "//*[@id='work_left']//div[contains(@class,'product-slider-data')]/div[1]");
            var thumbU = AbsUrl(GetAttr(firstThumbDiv, "data-thumb"), url);
            if (!string.IsNullOrEmpty(thumbU))
            {
                // BigThumbUrl ⇒ Backdrop
                meta.Backdrop = thumbU;
                AddThumb(meta, thumbU);
            }

            // 预览图（data-src / data-thumb）
            foreach (var node in SelectNodes(doc, "//*[@id='work_left']//div[contains(@class,'product-slider-data')]//div[@data-src or @data-thumb]"))
            {
                var u = AbsUrl(GetAttr(node, "data-src"), url);
                if (string.IsNullOrWhiteSpace(u)) u = AbsUrl(GetAttr(node, "data-thumb"), url);
                AddThumb(meta, u);
            }

            // 去掉与 Primary/Backdrop 重复
            meta.Thumbnails = meta.Thumbnails
                .Where(t => !string.IsNullOrEmpty(t) && !UrlEq(t, meta.Primary) && !UrlEq(t, meta.Backdrop))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 评分（0–5）
            try
            {
                var score = await FetchRateAverage2dpAsync(url, id, ct);
                if (score > 0) meta.Rating = score;
            }
            catch (Exception ex) { _logger.LogDebug(ex, "评分抓取失败：{Id}", id); }

            return meta;
        }

        // ================= 搜索页（收集 ID） =================

        private async Task<List<string>> CollectIdsFromSearchAsync(string normalizedKw, CancellationToken ct)
        {
            var url = string.Format(CultureInfo.InvariantCulture, UnifiedSearchUrl, Uri.EscapeDataString(normalizedKw));
            _logger.LogInformation("DLsite 搜索: {Url}", url);

            var html = await GetStringAsync(url, ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var a in SelectNodes(doc, "//div[@id='search_result_list']//li//a[contains(@href,'/work/=/product_id/')]"))
            {
                var href = AbsUrl(a.GetAttributeValue("href", ""), url);
                if (!Uri.TryCreate(href, UriKind.Absolute, out var uri)) continue;
                var baseName = System.IO.Path.GetFileName(uri.AbsolutePath);
                var id = baseName.Contains('.') ? baseName[..baseName.LastIndexOf('.')] : baseName;
                id = id.Trim().ToUpperInvariant();
                if ((!RjRe.IsMatch(id)) && (!VjRe.IsMatch(id))) continue;
                if (seen.Add(id)) ids.Add(id);
            }
            return ids;
        }

        // ================= 人员抽取（简洁但覆盖面广） =================

        private static void ExtractPeople(HtmlDocument doc, HanimeMetadata meta)
        {
            // 演员（声優/出演者/キャスト 第一行）
            foreach (var a in SelectNodes(doc, "(//*[@id='work_right']//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'声優') or contains(normalize-space(.),'出演者') or contains(normalize-space(.),'キャスト')]])[1]//td//a"))
            {
                var name = Clean(a.InnerText);
                if (!string.IsNullOrEmpty(name))
                    AddPerson(meta, name, "Actor", null);
            }

            // 常见职能：导演/脚本/原画/制作/监督/企画 等
            // 扫描 outline 表格的所有行，读 th 文本作为“角色类型”
            foreach (var tr in SelectNodes(doc, "//table[@id='work_outline']//tr"))
            {
                var th = tr.SelectSingleNode(".//th");
                var td = tr.SelectSingleNode(".//td");
                if (th == null || td == null) continue;

                var roleRaw = Clean(th.InnerText);
                if (string.IsNullOrEmpty(roleRaw)) continue;

                // 已由上面的“演员”处理过的类别跳过
                if (roleRaw.Contains("声優") || roleRaw.Contains("出演者") || roleRaw.Contains("キャスト"))
                    continue;

                var names = td.SelectNodes(".//a")?.Select(x => Clean(x.InnerText)).Where(s => !string.IsNullOrEmpty(s)).ToList()
                           ?? new List<string> { Clean(td.InnerText) };

                var (type, subRole) = MapRole(roleRaw);

                foreach (var n in names)
                {
                    if (!string.IsNullOrEmpty(n))
                        AddPerson(meta, n, type, subRole);
                }
            }
        }

        private static (string Type, string? Role) MapRole(string header)
        {
            // 常见映射（英文化），其余保留原文作为 Type
            if (header.Contains("監督") || header.Contains("ディレクター")) return ("Director", null);
            if (header.Contains("シナリオ") || header.Contains("脚本")) return ("Writer", null);
            if (header.Contains("原画") || header.Contains("イラスト")) return ("Illustrator", null);
            if (header.Contains("制作") || header.Contains("企画") || header.Contains("プロデューサ"))
                return ("Producer", null);
            if (header.Contains("編集")) return ("Editor", null);
            if (header.Contains("音楽")) return ("Composer", null);
            // 未识别：直接把表头作为 Type 存起来（满足“将类型存下来”的要求）
            return (header, null);
        }

        private static void AddPerson(HanimeMetadata meta, string name, string type, string? role)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            // 去重：按 Name+Type
            if (meta.People.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                                   && p.Type.Equals(type, StringComparison.OrdinalIgnoreCase)))
                return;
            meta.People.Add(new PersonDto { Name = name, Type = type, Role = role });
        }

        // ================= 评分 AJAX =================

        private static async Task<double> FetchRateAverage2dpAsync(string homepageUrl, string id, CancellationToken ct)
        {
            var site = homepageUrl.Contains("/pro/", StringComparison.OrdinalIgnoreCase) ? "pro" : "maniax";
            var ajaxUrl = $"https://www.dlsite.com/{site}/product/info/ajax?product_id={Uri.EscapeDataString(id)}";

            using var req = new HttpRequestMessage(HttpMethod.Get, ajaxUrl);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");
            req.Headers.Referrer = new Uri(homepageUrl);

            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            await using var s = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);

            if (doc.RootElement.TryGetProperty(id, out var entry)
                && entry.TryGetProperty("rate_average_2dp", out var v)
                && v.ValueKind == JsonValueKind.Number
                && v.TryGetDouble(out var f))
            {
                return f; // 0–5
            }
            return 0d;
        }

        // ================= 迷你工具函数（尽量精简） =================

        private static string NormalizeKeyword(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            // 保留字母/数字/空格/下划线/连字符，其他替空格
            var arr = s.Trim().ToCharArray();
            for (int i = 0; i < arr.Length; i++)
            {
                var r = arr[i];
                if (char.IsLetterOrDigit(r) || char.IsWhiteSpace(r) || r == '_' || r == '-') continue;
                arr[i] = ' ';
            }
            var cleaned = new string(arr);
            return SpaceCollapseRe.Replace(cleaned, " ").Trim().Replace(' ', '+');
        }

        private static string BuildQueryFromFilename(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var name = System.IO.Path.GetFileNameWithoutExtension(s.Trim());
            var cleaned = Regex.Replace(name, @"(?i)\b(1080p|2160p|720p|480p|hevc|x26[45]|h\.?26[45]|aac|flac|hdr|dv|10bit|8bit|webrip|web-dl|bluray|remux|sub|chs|cht|eng|multi|unrated|proper|repack)\b", " ");
            cleaned = Regex.Replace(cleaned, @"[\[\]\(\)\{\}【】（）]", " ");
            cleaned = Regex.Replace(cleaned, @"[_\.]+", " ");
            return SpaceCollapseRe.Replace(cleaned, " ").Trim();
        }

        private static bool TryParseDlsiteId(string input, out string id)
        {
            id = "";
            var t = input?.Trim() ?? "";
            if (RjRe.IsMatch(t) || VjRe.IsMatch(t)) { id = t.ToUpperInvariant(); return true; }

            var parsed = ParseMovieIDFromURL(t);
            if (!string.IsNullOrEmpty(parsed)) { id = parsed; return true; }
            return false;
        }

        public static string ParseMovieIDFromURL(string rawUrl)
        {
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)) return "";
            var baseName = System.IO.Path.GetFileName(uri.AbsolutePath);
            if (ProductPathRe.IsMatch(baseName))
            {
                var id = baseName[..baseName.LastIndexOf('.')];
                return id.Trim().ToUpperInvariant();
            }
            foreach (var seg in uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (RjRe.IsMatch(seg) || VjRe.IsMatch(seg)) return seg.Trim().ToUpperInvariant();
            }
            return "";
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = WebUtility.HtmlDecode(s).Replace('　', ' ').Trim();
            return SpaceCollapseRe.Replace(s, " ");
        }

        private static string AbsUrl(string possiblyRelative, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(possiblyRelative)) return "";
            if (possiblyRelative.StartsWith("//")) return "https:" + possiblyRelative;
            if (Uri.TryCreate(possiblyRelative, UriKind.Absolute, out var abs)) return abs.ToString();
            if (Uri.TryCreate(new Uri(baseUrl), possiblyRelative, out var rel)) return rel.ToString();
            return possiblyRelative;
        }

        private static string GetAttr(HtmlNode? n, string name) => n?.GetAttributeValue(name, "") ?? "";

        private static HtmlNode? SelectSingle(HtmlDocument d, string xp) => d.DocumentNode.SelectSingleNode(xp);
        private static HtmlNodeCollection? SelectNodes(HtmlDocument d, string xp) => d.DocumentNode.SelectNodes(xp);
        private static string SelectText(HtmlDocument d, string xp) => d.DocumentNode.SelectSingleNode(xp)?.InnerText?.Trim() ?? "";

        private static string ExtractOutlineCell(HtmlDocument d, string xp)
        {
            var td = d.DocumentNode.SelectSingleNode(xp);
            if (td == null) return "";
            var a1 = td.SelectSingleNode(".//a[1]");
            var val = Clean(a1?.InnerText ?? td.InnerText);
            return val;
        }
        private static string ExtractOutlineCellPreferA(HtmlDocument d, string xp)
        {
            var td = d.DocumentNode.SelectSingleNode(xp);
            if (td == null) return "";
            var a = td.SelectSingleNode(".//a");
            return Clean(a?.InnerText ?? td.InnerText);
        }

        private static string ExtractSummary(HtmlDocument doc)
        {
            var node = SelectSingle(doc, "//div[@itemprop='description' and contains(@class,'work_parts_container')]");
            if (node == null) return "";
            var paras = new List<string>();

            foreach (var item in node.SelectNodes(".//div[contains(@class,'work_parts_multitype_item') and contains(@class,'type_text')]") ?? new HtmlNodeCollection(null))
            {
                var ps = item.SelectNodes(".//p");
                if (ps != null && ps.Count > 0)
                    foreach (var p in ps) AddPara(paras, TextWithBr(p));
                else
                    AddPara(paras, TextWithBr(item));
            }
            foreach (var p in node.SelectNodes(".//div[contains(@class,'work_parts_area')]//p") ?? new HtmlNodeCollection(null))
                AddPara(paras, TextWithBr(p));

            var s = string.Join("\n\n", paras.Where(x => !string.IsNullOrWhiteSpace(x)));
            s = SpaceCollapseRe.Replace(s, " ");
            return s.Trim();

            static void AddPara(List<string> list, string? raw)
            {
                raw = (raw ?? "").Trim();
                if (!string.IsNullOrEmpty(raw)) list.Add(raw);
            }
            static string TextWithBr(HtmlNode n)
            {
                var sb = new StringBuilder();
                void Walk(HtmlNode x)
                {
                    if (x.NodeType == HtmlNodeType.Text) sb.Append(x.InnerText);
                    else if (string.Equals(x.Name, "br", StringComparison.OrdinalIgnoreCase)) sb.Append('\n');
                    foreach (var c in x.ChildNodes) Walk(c);
                }
                Walk(n);
                return WebUtility.HtmlDecode(sb.ToString());
            }
        }

        private static void AddThumb(HanimeMetadata meta, string? u)
        {
            if (string.IsNullOrWhiteSpace(u)) return;
            if (u.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                var idx = u.LastIndexOf('.');
                if (idx > 0) u = u[..idx] + ".jpg";
            }
            if (!meta.Thumbnails.Contains(u)) meta.Thumbnails.Add(u);
        }

        private static string PickJpg(string src, string srcset, string baseUrl)
        {
            string Try(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return "";
                var abs = AbsUrl(s, baseUrl);
                if (abs.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) return abs;
                if (abs.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    var i = abs.LastIndexOf('.');
                    return i > 0 ? abs[..i] + ".jpg" : abs;
                }
                return "";
            }
            var a = Try(src);
            if (!string.IsNullOrEmpty(a)) return a;

            foreach (var part in (srcset ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = part.Trim().Split(' ')[0];
                var m2 = Try(p);
                if (!string.IsNullOrEmpty(m2)) return m2;
            }
            return "";
        }

        private static bool UrlEq(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<string> GetStringAsync(string url, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct);
        }
    }
}
