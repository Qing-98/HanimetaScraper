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

    // Shared mode
    private IBrowserContext? _sharedCtx;
    private DateTime _sharedBirth = DateTime.MinValue;
    private volatile int _sharedOpenedPages = 0;
    private volatile int _sharedActivePages = 0; // Count of pages currently being used
    private volatile bool _sharedFlagged = false;

    // Split mode
    private IBrowserContext? _searchCtx;
    private DateTime _searchBirth = DateTime.MinValue;
    private volatile int _searchOpenedPages = 0;
    private volatile int _searchActivePages = 0;
    private volatile bool _searchFlagged = false;

    private IBrowserContext? _detailCtx;
    private DateTime _detailBirth = DateTime.MinValue;
    private volatile int _detailOpenedPages = 0;
    private volatile int _detailActivePages = 0;
    private volatile bool _detailFlagged = false;

    public PlaywrightContextManager(IBrowser browser, ILogger logger, PlaywrightClientOptions? opt = null)
    {
        _browser = browser;
        _logger = logger;
        _opt = opt ?? new PlaywrightClientOptions();
        
        // 初始化 Cookie 持久化管理器
        if (_opt.EnableCookiePersistence)
        {
            _cookieManager = new CookiePersistenceManager(_logger, _opt.CookieStorageDirectory);
            _logger.LogSuccess("CookiePersistence", "Cookie 持久化已启用");
        }
    }

    public PlaywrightClientOptions Options => _opt;

    /// <summary>The underlying browser instance.</summary>
    public IBrowser Browser => _browser;
    
    /// <summary>获取 Cookie 管理器 (如果已启用)</summary>
    public CookiePersistenceManager? CookieManager => _cookieManager;

    /// <summary>External API: get available context; forDetail indicates getting "detail-use" context.</summary>
    public async Task<IBrowserContext> GetOrCreateContextAsync(bool forDetail)
    {
        ThrowIfDisposed();
        
        await _contextLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_opt.IsolationMode == ContextIsolationMode.Shared)
            {
                bool alive = _sharedCtx?.Browser?.IsConnected ?? false;
                if (!alive || (ShouldRotate(_sharedBirth, _sharedOpenedPages, _sharedFlagged) && _sharedActivePages == 0))
                {
                    await SafeCloseAsync(_sharedCtx);
                    _sharedCtx = await NewIsolatedContextAsync();
                    _sharedBirth = DateTime.UtcNow;
                    _sharedOpenedPages = 0;
                    _sharedActivePages = 0;
                    _sharedFlagged = false;
                    _logger.LogResourceEvent("BrowserContext", "Created (Shared)");
                }
                Interlocked.Increment(ref _sharedActivePages);
                return _sharedCtx!;
            }
            else
            {
                if (!forDetail)
                {
                    bool alive = _searchCtx?.Browser?.IsConnected ?? false;
                    if (!alive || (ShouldRotate(_searchBirth, _searchOpenedPages, _searchFlagged) && _searchActivePages == 0))
                    {
                        await SafeCloseAsync(_searchCtx);
                        _searchCtx = await NewIsolatedContextAsync();
                        _searchBirth = DateTime.UtcNow;
                        _searchOpenedPages = 0;
                        _searchActivePages = 0;
                        _searchFlagged = false;
                        _logger.LogResourceEvent("BrowserContext", "Created (Search)");
                    }
                    Interlocked.Increment(ref _searchActivePages);
                    return _searchCtx!;
                }
                else
                {
                    bool alive = _detailCtx?.Browser?.IsConnected ?? false;
                    if (!alive || (ShouldRotate(_detailBirth, _detailOpenedPages, _detailFlagged) && _detailActivePages == 0))
                    {
                        await SafeCloseAsync(_detailCtx);
                        _detailCtx = await NewIsolatedContextAsync();
                        _detailBirth = DateTime.UtcNow;
                        _detailOpenedPages = 0;
                        _detailActivePages = 0;
                        _detailFlagged = false;
                        _logger.LogResourceEvent("BrowserContext", "Created (Detail)");
                    }
                    Interlocked.Increment(ref _detailActivePages);
                    return _detailCtx!;
                }
            }
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
            if (ReferenceEquals(ctx, _sharedCtx)) Interlocked.Increment(ref _sharedOpenedPages);
        }
        else
        {
            if (ReferenceEquals(ctx, _searchCtx)) Interlocked.Increment(ref _searchOpenedPages);
            else if (ReferenceEquals(ctx, _detailCtx)) Interlocked.Increment(ref _detailOpenedPages);
        }
    }

    /// <summary>Decrement active page count when page is closed.</summary>
    public void DecrementActivePages(IBrowserContext ctx, bool forDetail)
    {
        if (_disposed) return; // Don't throw if already disposed
        
        if (_opt.IsolationMode == ContextIsolationMode.Shared)
        {
            if (ReferenceEquals(ctx, _sharedCtx)) Interlocked.Decrement(ref _sharedActivePages);
        }
        else
        {
            if (ReferenceEquals(ctx, _searchCtx)) Interlocked.Decrement(ref _searchActivePages);
            else if (ReferenceEquals(ctx, _detailCtx)) Interlocked.Decrement(ref _detailActivePages);
        }
    }

    /// <summary>Mark current context as encountering challenge (triggers subsequent rotation).</summary>
    public void FlagChallengeOnCurrent(bool forDetail)
    {
        ThrowIfDisposed();
        
        if (_opt.IsolationMode == ContextIsolationMode.Shared)
        {
            _sharedFlagged = true;
        }
        else
        {
            if (forDetail) _detailFlagged = true; else _searchFlagged = true;
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
            {
                // Close existing context immediately
                await SafeCloseAsync(_sharedCtx);
                _sharedCtx = await NewIsolatedContextAsync();
                _sharedBirth = DateTime.UtcNow;
                _sharedOpenedPages = 0;
                _sharedActivePages = 0;
                _sharedFlagged = false;
                _logger.LogResourceEvent("BrowserContext", "Force rotated (Shared)");
            }
            else
            {
                if (!forDetail)
                {
                    await SafeCloseAsync(_searchCtx);
                    _searchCtx = await NewIsolatedContextAsync();
                    _searchBirth = DateTime.UtcNow;
                    _searchOpenedPages = 0;
                    _searchActivePages = 0;
                    _searchFlagged = false;
                    _logger.LogResourceEvent("BrowserContext", "Force rotated (Search)");
                }
                else
                {
                    await SafeCloseAsync(_detailCtx);
                    _detailCtx = await NewIsolatedContextAsync();
                    _detailBirth = DateTime.UtcNow;
                    _detailOpenedPages = 0;
                    _detailActivePages = 0;
                    _detailFlagged = false;
                    _logger.LogResourceEvent("BrowserContext", "Force rotated (Detail)");
                }
            }
        }
        finally
        {
            _contextLock.Release();
        }
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
                        "已加载 cookies 到新上下文",
                        savedCookies.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("CookiePersistence", "加载 cookies 失败, 继续使用空上下文", null, ex);
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
                
                if (_sharedCtx != null)
                {
                    closeTasks.Add(SafeCloseAsync(_sharedCtx));
                    _logger.LogResourceEvent("BrowserContext", "Disposing (Shared)");
                }
                
                if (_searchCtx != null)
                {
                    closeTasks.Add(SafeCloseAsync(_searchCtx));
                    _logger.LogResourceEvent("BrowserContext", "Disposing (Search)");
                }
                
                if (_detailCtx != null)
                {
                    closeTasks.Add(SafeCloseAsync(_detailCtx));
                    _logger.LogResourceEvent("BrowserContext", "Disposing (Detail)");
                }

                // Wait for all contexts to close
                if (closeTasks.Count > 0)
                {
                    await Task.WhenAll(closeTasks).ConfigureAwait(false);
                }

                _sharedCtx = null;
                _searchCtx = null;
                _detailCtx = null;
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
}
