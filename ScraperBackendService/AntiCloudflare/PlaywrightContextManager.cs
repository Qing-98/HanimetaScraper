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
        Shared,            // Search and detail share the same context (default)
        SplitSearchDetail  // Search and detail are separated, detail context can be rotated independently
    }

    public sealed class ScrapeRuntimeOptions
    {
        // —— Rotation Conditions ——
        public int ContextTtlMinutes { get; set; } = 8;     // TTL expiration rotation
        public int MaxPagesPerContext { get; set; } = 50;   // Maximum pages per context
        public bool RotateOnChallengeDetected { get; set; } = true;

        // —— Isolation Strategy ——
        public ContextIsolationMode IsolationMode { get; set; } = ContextIsolationMode.Shared;

        // —— Slow Retry Parameters (for upper layer use) ——
        public int SlowRetryGotoTimeoutMs { get; set; } = 90000;
        public int SlowRetryWaitSelectorMs { get; set; } = 15000;

        // —— Cloudflare/Challenge Recognition Hints —— 
        public string[] ChallengeUrlHints { get; set; } = new[] { "challenge", "cf-challenge", "cloudflare", "/cdn-cgi/" };
        public string[] ChallengeDomHints { get; set; } = new[] { "cf-challenge", "#challenge-form", "Just a moment", "Verifying you are human" };

        // —— Basic Fingerprint Configuration —— 
        public string UserAgent { get; set; } =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";
        public int ViewportWidth { get; set; } = 1280;
        public int ViewportHeight { get; set; } = 900;
        public string Locale { get; set; } = "zh-CN";
        public string TimezoneId { get; set; } = "Australia/Melbourne";
        public string AcceptLanguage { get; set; } = "zh-CN,zh;q=0.9";
        public string? StealthInitRelativePath { get; set; } = Path.Combine("AntiCloudflare", "StealthInit.js");

        // Page ready determination: wait for these selectors in sequence; fallback to "body" when null/empty
        public string[]? ReadySelectors { get; set; }

    }


    /// <summary>
    /// Responsible for creating/reusing/rotating Playwright BrowserContext (including search/detail isolation).
    /// </summary>
    public class PlaywrightContextManager
    {
        private readonly IBrowser _browser;
        private readonly ILogger _logger;
        private readonly ScrapeRuntimeOptions _opt;

        // Shared mode
        private IBrowserContext? _sharedCtx;
        private DateTime _sharedBirth = DateTime.MinValue;
        private int _sharedOpenedPages = 0;
        private volatile bool _sharedFlagged = false;

        // Split mode
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

        /// <summary>External API: get available context; forDetail indicates getting "detail-use" context.</summary>
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

        /// <summary>Statistics: call once for each page opened.</summary>
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

        /// <summary>Mark current context as encountering challenge (triggers subsequent rotation).</summary>
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

            // Inject Stealth script (optional)
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
