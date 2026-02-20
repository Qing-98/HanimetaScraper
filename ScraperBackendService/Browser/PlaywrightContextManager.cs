using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using System.Threading;
using ScraperBackendService.Core.Logging;
using ScraperBackendService.Core.Net;
using ScraperBackendService.Browser;

namespace ScraperBackendService.Browser;

/// <summary>
/// Responsible for creating/reusing/rotating Playwright BrowserContext (including search/detail isolation).
/// </summary>
public class PlaywrightContextManager : IAsyncDisposable, IDisposable
{
    private readonly IBrowser _browser;
    private readonly ILogger _logger;
    private readonly PlaywrightClientOptions _opt;
    private readonly CookiePersistenceManager? _cookieManager;
    private readonly SemaphoreSlim _contextLock = new(1, 1);
    private bool _disposed = false;

    private readonly ContextSlotState _shared = new();
    private readonly ContextSlotState _search = new();
    private readonly ContextSlotState _detail = new();

    public PlaywrightContextManager(IBrowser browser, ILogger logger, PlaywrightClientOptions? opt = null)
    {
        _browser = browser;
        _logger = logger;
        _opt = opt ?? new PlaywrightClientOptions();
        
        // Initialize cookie persistence manager
        if (_opt.EnableCookiePersistence)
        {
            _cookieManager = new CookiePersistenceManager(_logger, _opt.CookieStorageDirectory);
            _logger.LogSuccess("CookiePersistence", "Cookie persistence enabled");
        }
    }

    public PlaywrightClientOptions Options => _opt;

    /// <summary>The underlying browser instance.</summary>
    public IBrowser Browser => _browser;
    
    /// <summary>Gets the cookie persistence manager (if enabled).</summary>
    public CookiePersistenceManager? CookieManager => _cookieManager;

    /// <summary>External API: get available context; forDetail indicates getting "detail-use" context.</summary>
    public async Task<IBrowserContext> GetOrCreateContextAsync(bool forDetail)
    {
        ThrowIfDisposed();

        await _contextLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_opt.IsolationMode == ContextIsolationMode.Shared)
                return await EnsureSlotContextAsync(_shared, "Shared");

            return forDetail
                ? await EnsureSlotContextAsync(_detail, "Detail")
                : await EnsureSlotContextAsync(_search, "Search");
        }
        finally
        {
            _contextLock.Release();
        }
    }

    /// <summary>Statistics: call once for each page opened.</summary>
    public void BumpOpenedPages(IBrowserContext ctx, bool forDetail)
    {
        ThrowIfDisposed();
        
        if (_opt.IsolationMode == ContextIsolationMode.Shared)
        {
            if (ReferenceEquals(ctx, _shared.Ctx)) Interlocked.Increment(ref _shared.OpenedPages);
        }
        else
        {
            if (ReferenceEquals(ctx, _search.Ctx)) Interlocked.Increment(ref _search.OpenedPages);
            else if (ReferenceEquals(ctx, _detail.Ctx)) Interlocked.Increment(ref _detail.OpenedPages);
        }
    }

    /// <summary>Decrement active page count when page is closed.</summary>
    public void DecrementActivePages(IBrowserContext ctx, bool forDetail)
    {
        if (_disposed) return; // Don't throw if already disposed
        
        if (_opt.IsolationMode == ContextIsolationMode.Shared)
        {
            if (ReferenceEquals(ctx, _shared.Ctx)) Interlocked.Decrement(ref _shared.ActivePages);
        }
        else
        {
            if (ReferenceEquals(ctx, _search.Ctx)) Interlocked.Decrement(ref _search.ActivePages);
            else if (ReferenceEquals(ctx, _detail.Ctx)) Interlocked.Decrement(ref _detail.ActivePages);
        }
    }

    /// <summary>Mark current context as encountering challenge (triggers subsequent rotation).</summary>
    public void FlagChallengeOnCurrent(bool forDetail)
    {
        ThrowIfDisposed();
        
        if (_opt.IsolationMode == ContextIsolationMode.Shared)
        {
            _shared.Flagged = true;
        }
        else
        {
            if (forDetail) _detail.Flagged = true; else _search.Flagged = true;
        }
    }

    /// <summary>
    /// Force immediate context rotation even if there are active pages.
    /// Used when challenge is detected and we need a fresh context immediately.
    /// </summary>
    /// <param name="forDetail">Whether to rotate detail or search context</param>
    public async Task ForceRotateContextAsync(bool forDetail)
    {
        ThrowIfDisposed();

        await _contextLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_opt.IsolationMode == ContextIsolationMode.Shared)
                await ResetSlotAsync(_shared, "Shared", "Force rotated");
            else if (forDetail)
                await ResetSlotAsync(_detail, "Detail", "Force rotated");
            else
                await ResetSlotAsync(_search, "Search", "Force rotated");
        }
        finally
        {
            _contextLock.Release();
        }
    }

    /// <summary>Resets a slot: closes old context, creates fresh one, resets counters.</summary>
    private async Task ResetSlotAsync(ContextSlotState slot, string label, string action)
    {
        await SafeCloseAsync(slot.Ctx);
        slot.Ctx = await NewIsolatedContextAsync();
        slot.Birth = DateTime.UtcNow;
        slot.OpenedPages = 0;
        slot.ActivePages = 0;
        slot.Flagged = false;
        _logger.LogResourceEvent("BrowserContext", $"{action} ({label})");
    }

    /// <summary>Ensures a slot has a live context (creating/rotating as needed) and increments ActivePages.</summary>
    private async Task<IBrowserContext> EnsureSlotContextAsync(ContextSlotState slot, string label)
    {
        bool alive = slot.Ctx?.Browser?.IsConnected ?? false;
        if (!alive || (ShouldRotate(slot.Birth, slot.OpenedPages, slot.Flagged) && slot.ActivePages == 0))
        {
            await ResetSlotAsync(slot, label, "Created");
        }
        Interlocked.Increment(ref slot.ActivePages);
        return slot.Ctx!;
    }

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

        // Inject Stealth script (optional or fallback)
        try
        {
            bool stealthInjected = false;
            if (!string.IsNullOrWhiteSpace(_opt.InitScriptPath))
            {
                var path = Path.IsPathRooted(_opt.InitScriptPath)
                    ? _opt.InitScriptPath
                    : Path.Combine(AppContext.BaseDirectory, _opt.InitScriptPath);
                if (File.Exists(path))
                {
                    var script = await File.ReadAllTextAsync(path);
                    await ctx.AddInitScriptAsync(script);
                    stealthInjected = true;
                }
            }

            if (!stealthInjected)
            {
                // Fallback to inline minimal stealth script if external file not found
                // Mimics ManualChallengeHandler logic for consistency
                await ctx.AddInitScriptAsync(@"
                    (() => {
                        delete navigator.webdriver;
                        Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                        if (navigator.plugins.length === 0) {
                            const _p = [
                                { name: 'Chrome PDF Plugin', filename: 'internal-pdf-viewer', description: 'Portable Document Format', length: 1 },
                                { name: 'Chrome PDF Viewer', filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai', description: '', length: 1 },
                                { name: 'Native Client', filename: 'internal-nacl-plugin', description: '', length: 2 },
                            ];
                            Object.defineProperty(navigator, 'plugins', { get: () => _p });
                        }
                        if (!navigator.languages || navigator.languages.length === 0) {
                            Object.defineProperty(navigator, 'languages', { get: () => ['zh-CN', 'zh', 'en'] });
                        }
                        if (!window.chrome) {
                            const chrome = { runtime: {}, loadTimes: function() {}, csi: function() {}, app: {} };
                            Object.defineProperty(window, 'chrome', { get: () => chrome });
                        }
                        if (window.navigator.permissions) {
                            const originalQuery = window.navigator.permissions.query;
                            window.navigator.permissions.query = (parameters) => (
                                parameters.name === 'notifications' ?
                                    Promise.resolve({ state: Notification.permission }) :
                                    originalQuery(parameters)
                            );
                        }
                        if (!navigator.connection) {
                            Object.defineProperty(navigator, 'connection', {
                                get: () => ({ rtt: 50, downlink: 10, effectiveType: '4g', saveData: false })
                            });
                        }
                    })();
                ");
                _logger.LogDebug("StealthScript", "Injected inline fallback stealth script");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("StealthScript", "Failed to inject stealth script, continuing without it", null, ex);
        }

        // Load saved cookies (if enabled)
        if (_opt.EnableCookiePersistence && _opt.AutoLoadCookiesOnContextCreation && _cookieManager != null)
        {
            try
            {
                var savedCookies = await _cookieManager.LoadAllCookiesAsync();
                if (savedCookies.Count > 0)
                {
                    await ctx.AddCookiesAsync(savedCookies);
                    _logger.LogSuccess("CookiePersistence",
                        "Loaded cookies into new context",
                        savedCookies.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("CookiePersistence", "Failed to load cookies, continuing with empty context", null, ex);
            }
        }

        return ctx;
    }

    /// <summary>
    /// Dispose all resources asynchronously. Preferred method for cleanup.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try
        {
            await _contextLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Close all browser contexts
                var closeTasks = new List<Task>();
                
                if (_shared.Ctx != null)
                {
                    closeTasks.Add(SafeCloseAsync(_shared.Ctx));
                    _logger.LogResourceEvent("BrowserContext", "Disposing (Shared)");
                }

                if (_search.Ctx != null)
                {
                    closeTasks.Add(SafeCloseAsync(_search.Ctx));
                    _logger.LogResourceEvent("BrowserContext", "Disposing (Search)");
                }

                if (_detail.Ctx != null)
                {
                    closeTasks.Add(SafeCloseAsync(_detail.Ctx));
                    _logger.LogResourceEvent("BrowserContext", "Disposing (Detail)");
                }

                // Wait for all contexts to close
                if (closeTasks.Count > 0)
                {
                    await Task.WhenAll(closeTasks).ConfigureAwait(false);
                }

                _shared.Ctx = null;
                _search.Ctx = null;
                _detail.Ctx = null;
            }
            finally
            {
                _contextLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("ContextManager", "Error during async dispose", null, ex);
        }
        finally
        {
            _contextLock?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Synchronous dispose method. Calls DisposeAsync().GetAwaiter().GetResult().
    /// </summary>
    public void Dispose()
    {
        try
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogDebug("ContextManager", "Error during synchronous dispose", null, ex);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PlaywrightContextManager));
        }
    }

    private static async Task SafeCloseAsync(IBrowserContext? ctx)
    {
        if (ctx is null) return;
        try 
        {
             // IBrowserContext doesn't have IsClosed property, so we just try to close it
            await ctx.CloseAsync().ConfigureAwait(false);
        } 
        catch 
        {
             /* ignore close errors */
        }
    }

    /// <summary>Encapsulates per-context lifecycle state for one isolation slot.</summary>
    private sealed class ContextSlotState
    {
        public IBrowserContext? Ctx;
        public DateTime Birth = DateTime.MinValue;
        public volatile int OpenedPages;
        public volatile int ActivePages;
        public volatile bool Flagged;
    }
}
