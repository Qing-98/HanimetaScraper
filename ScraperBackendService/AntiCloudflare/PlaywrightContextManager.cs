using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace ScraperBackendService.AntiCloudflare
{
    public enum ContextIsolationMode
    {
        Shared,            // 搜索与详情共用同一个 Context（默认）
        SplitSearchDetail  // 搜索与详情分离，详情 Context 可单独轮换
    }

    public sealed class ScrapeRuntimeOptions
    {
        // —— 轮换条件 ——
        public int ContextTtlMinutes { get; set; } = 8;     // TTL 到期轮换
        public int MaxPagesPerContext { get; set; } = 50;   // 一个 Context 内最多开多少 Page
        public bool RotateOnChallengeDetected { get; set; } = true;

        // —— 隔离策略 ——
        public ContextIsolationMode IsolationMode { get; set; } = ContextIsolationMode.Shared;

        // —— 慢速重试参数（供上层用） ——
        public int SlowRetryGotoTimeoutMs { get; set; } = 90000;
        public int SlowRetryWaitSelectorMs { get; set; } = 15000;

        // —— Cloudflare/Challenge 识别提示 —— 
        public string[] ChallengeUrlHints { get; set; } = new[] { "challenge", "cf-challenge", "cloudflare", "/cdn-cgi/" };
        public string[] ChallengeDomHints { get; set; } = new[] { "cf-challenge", "#challenge-form", "Just a moment", "Verifying you are human" };

        // —— 基础指纹配置 —— 
        public string UserAgent { get; set; } =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";
        public int ViewportWidth { get; set; } = 1280;
        public int ViewportHeight { get; set; } = 900;
        public string Locale { get; set; } = "zh-CN";
        public string TimezoneId { get; set; } = "Australia/Melbourne";
        public string AcceptLanguage { get; set; } = "zh-CN,zh;q=0.9";
        public string? StealthInitRelativePath { get; set; } = Path.Combine("AntiCloudflare", "StealthInit.js");
    }

    /// <summary>
    /// 负责创建/复用/轮换 Playwright 的 BrowserContext（含搜索/详情隔离）。
    /// </summary>
    public class PlaywrightContextManager
    {
        private readonly IBrowser _browser;
        private readonly ILogger _logger;
        private readonly ScrapeRuntimeOptions _opt;

        // Shared 模式
        private IBrowserContext? _sharedCtx;
        private DateTime _sharedBirth = DateTime.MinValue;
        private int _sharedOpenedPages = 0;
        private volatile bool _sharedFlagged = false;

        // Split 模式
        private IBrowserContext? _searchCtx;
        private DateTime _searchBirth = DateTime.MinValue;
        private int _searchOpenedPages = 0;
        private volatile bool _searchFlagged = false;

        private IBrowserContext? _detailCtx;
        private DateTime _detailBirth = DateTime.MinValue;
        private int _detailOpenedPages = 0;
        private volatile bool _detailFlagged = false;

        public PlaywrightContextManager(IBrowser browser, ILogger logger, ScrapeRuntimeOptions? opt = null)
        {
            _browser = browser;
            _logger = logger;
            _opt = opt ?? new ScrapeRuntimeOptions();
        }

        /// <summary>对外：拿到可用的 Context；forDetail 表示拿“详情用”的 Context。</summary>
        public async Task<IBrowserContext> GetOrCreateContextAsync(bool forDetail)
        {
            if (_opt.IsolationMode == ContextIsolationMode.Shared)
            {
                bool alive = _sharedCtx?.Browser?.IsConnected ?? false;
                if (!alive || ShouldRotate(_sharedBirth, _sharedOpenedPages, _sharedFlagged))
                {
                    await SafeCloseAsync(_sharedCtx);
                    _sharedCtx = await NewIsolatedContextAsync();
                    _sharedBirth = DateTime.UtcNow;
                    _sharedOpenedPages = 0;
                    _sharedFlagged = false;
                    _logger.LogInformation("PlaywrightContextManager: Shared context (re)created.");
                }
                return _sharedCtx!;
            }
            else
            {
                if (!forDetail)
                {
                    bool alive = _searchCtx?.Browser?.IsConnected ?? false;
                    if (!alive || ShouldRotate(_searchBirth, _searchOpenedPages, _searchFlagged))
                    {
                        await SafeCloseAsync(_searchCtx);
                        _searchCtx = await NewIsolatedContextAsync();
                        _searchBirth = DateTime.UtcNow;
                        _searchOpenedPages = 0;
                        _searchFlagged = false;
                        _logger.LogInformation("PlaywrightContextManager: Search context (re)created.");
                    }
                    return _searchCtx!;
                }
                else
                {
                    bool alive = _detailCtx?.Browser?.IsConnected ?? false;
                    if (!alive || ShouldRotate(_detailBirth, _detailOpenedPages, _detailFlagged))
                    {
                        await SafeCloseAsync(_detailCtx);
                        _detailCtx = await NewIsolatedContextAsync();
                        _detailBirth = DateTime.UtcNow;
                        _detailOpenedPages = 0;
                        _detailFlagged = false;
                        _logger.LogInformation("PlaywrightContextManager: Detail context (re)created.");
                    }
                    return _detailCtx!;
                }
            }
        }

        /// <summary>统计：每开一个 Page 调用一次。</summary>
        public void BumpOpenedPages(IBrowserContext ctx, bool forDetail)
        {
            if (_opt.IsolationMode == ContextIsolationMode.Shared)
            {
                if (ReferenceEquals(ctx, _sharedCtx)) _sharedOpenedPages++;
            }
            else
            {
                if (ReferenceEquals(ctx, _searchCtx)) _searchOpenedPages++;
                else if (ReferenceEquals(ctx, _detailCtx)) _detailOpenedPages++;
            }
        }

        /// <summary>标记当前上下文遇到挑战（触发后续轮换）。</summary>
        public void FlagChallengeOnCurrent(bool forDetail)
        {
            if (_opt.IsolationMode == ContextIsolationMode.Shared)
            {
                _sharedFlagged = true;
            }
            else
            {
                if (forDetail) _detailFlagged = true; else _searchFlagged = true;
            }
        }

        public ScrapeRuntimeOptions Options => _opt;

        private bool ShouldRotate(DateTime birth, int openedPages, bool flagged)
        {
            if (flagged && _opt.RotateOnChallengeDetected) return true;
            if (_opt.ContextTtlMinutes > 0 && DateTime.UtcNow - birth > TimeSpan.FromMinutes(_opt.ContextTtlMinutes)) return true;
            if (_opt.MaxPagesPerContext > 0 && openedPages >= _opt.MaxPagesPerContext) return true;
            return false;
        }

        private async Task<IBrowserContext> NewIsolatedContextAsync()
        {
            var ctx = await _browser.NewContextAsync(new()
            {
                UserAgent = _opt.UserAgent,
                ViewportSize = new() { Width = _opt.ViewportWidth, Height = _opt.ViewportHeight },
                Locale = _opt.Locale,
                TimezoneId = _opt.TimezoneId
            });
            await ctx.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
            {
                { "Accept-Language", _opt.AcceptLanguage }
            });

            // 注入 Stealth 脚本（可选）
            try
            {
                if (!string.IsNullOrWhiteSpace(_opt.StealthInitRelativePath))
                {
                    var path = Path.IsPathRooted(_opt.StealthInitRelativePath)
                        ? _opt.StealthInitRelativePath
                        : Path.Combine(AppContext.BaseDirectory, _opt.StealthInitRelativePath);

                    if (File.Exists(path))
                    {
                        var script = await File.ReadAllTextAsync(path);
                        await ctx.AddInitScriptAsync(script);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AddInitScript stealth failed, continue without it.");
            }

            return ctx;
        }

        private static async Task SafeCloseAsync(IBrowserContext? ctx)
        {
            if (ctx is null) return;
            try { await ctx.CloseAsync(); } catch { /* ignore */ }
        }
    }
}
