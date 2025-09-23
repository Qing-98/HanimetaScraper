using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScraperBackendService.AntiCloudflare;
using ScraperBackendService.Core.Abstractions;

namespace ScraperBackendService.Core.Net;

/// <summary>
/// Unified Playwright network client supporting two operational modes:
/// 1. Context Management Mode: Uses PlaywrightContextManager for context reuse and rotation (recommended)
/// 2. Simple Mode: Creates new context for each request (lightweight usage)
/// </summary>
/// <example>
/// Usage examples:
///
/// // Context Management Mode (recommended for production)
/// var contextManager = new PlaywrightContextManager(browser, logger, options);
/// var client = new PlaywrightNetworkClient(contextManager, logger, antiBotHook);
///
/// // Simple Mode (lightweight for testing)
/// var client = new PlaywrightNetworkClient(browser, options, antiBotHook);
///
/// // Basic usage
/// var html = await client.GetHtmlAsync("https://example.com", ct);
/// var json = await client.GetJsonAsync("https://api.example.com/data", headers, ct);
/// var page = await client.OpenPageAsync("https://dynamic-site.com", ct);
/// </example>
public sealed class PlaywrightNetworkClient : INetworkClient, IAsyncDisposable
{
    private readonly IBrowser _browser;
    private readonly PlaywrightContextManager? _ctxMgr;
    private readonly PlaywrightClientOptions _opt;
    private readonly ILogger? _log;
    private readonly Func<IPage, CancellationToken, Task>? _antiBotHook;
    private readonly bool _useContextManager;

    /// <summary>
    /// Context Management Mode constructor (recommended for production).
    /// Provides efficient context reuse and automatic rotation for anti-bot protection.
    /// </summary>
    /// <param name="contextManager">Context manager for browser context lifecycle management</param>
    /// <param name="logger">Logger for operation tracking and debugging</param>
    /// <param name="antiBotHook">Optional anti-bot actions to execute on pages</param>
    /// <example>
    /// var options = new ScrapeRuntimeOptions { ContextTtlMinutes = 30, MaxPagesPerContext = 50 };
    /// var contextManager = new PlaywrightContextManager(browser, logger, options);
    /// var client = new PlaywrightNetworkClient(contextManager, logger, async (page, ct) => {
    ///     await page.WaitForTimeoutAsync(1000); // Simple anti-bot delay
    /// });
    /// </example>
    public PlaywrightNetworkClient(
        PlaywrightContextManager contextManager,
        ILogger logger,
        Func<IPage, CancellationToken, Task>? antiBotHook = null)
    {
        _ctxMgr = contextManager;
        _browser = GetBrowserFromContextManager(contextManager);
        _opt = ConvertToPlaywrightClientOptions(contextManager.Options);
        _log = logger;
        _antiBotHook = antiBotHook;
        _useContextManager = true;
    }

    /// <summary>
    /// Simple Mode constructor for lightweight usage.
    /// Creates new browser context for each operation without reuse.
    /// </summary>
    /// <param name="browser">Playwright browser instance</param>
    /// <param name="options">Optional client configuration options</param>
    /// <param name="antiBotHook">Optional anti-bot actions to execute on pages</param>
    /// <example>
    /// var browser = await playwright.Chromium.LaunchAsync();
    /// var options = new PlaywrightClientOptions { UserAgent = "Custom Agent", ViewportWidth = 1920 };
    /// var client = new PlaywrightNetworkClient(browser, options);
    /// </example>
    public PlaywrightNetworkClient(
        IBrowser browser,
        PlaywrightClientOptions? options = null,
        Func<IPage, CancellationToken, Task>? antiBotHook = null)
    {
        _browser = browser;
        _opt = options ?? new PlaywrightClientOptions();
        _antiBotHook = antiBotHook;
        _useContextManager = false;
    }

    /// <summary>
    /// Retrieves HTML content from the specified URL using the configured client mode.
    /// </summary>
    /// <param name="url">Target URL to fetch HTML from</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>HTML content as string</returns>
    /// <example>
    /// var html = await client.GetHtmlAsync("https://example.com/page", CancellationToken.None);
    /// var doc = new HtmlDocument();
    /// doc.LoadHtml(html);
    /// var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText;
    /// </example>
    public async Task<string> GetHtmlAsync(string url, CancellationToken ct)
    {
        if (_useContextManager)
        {
            var page = await OpenPageWithContextManagerAsync(url, ct);
            if (page is null) return string.Empty;

            try
            {
                return await page.ContentAsync();
            }
            finally
            {
                await SafeClosePage(page);
            }
        }
        else
        {
            var (ctx, page) = await NewContextAndPageAsync(ct);
            try
            {
                await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = _opt.GotoTimeoutMs });
                await WaitReadyAsync(page, ct);
                if (_antiBotHook is not null) await _antiBotHook(page, ct);
                return await page.ContentAsync();
            }
            finally
            {
                await SafeClose(page, ctx);
            }
        }
    }

    /// <summary>
    /// Retrieves JSON data from the specified URL with optional custom headers.
    /// Context Management Mode uses HttpClient for efficiency, Simple Mode uses browser fetch.
    /// </summary>
    /// <param name="url">Target URL to fetch JSON from</param>
    /// <param name="headers">Optional HTTP headers</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Parsed JSON document</returns>
    /// <example>
    /// // Simple JSON request
    /// var json = await client.GetJsonAsync("https://api.example.com/data", null, ct);
    /// var value = json.RootElement.GetProperty("result").GetString();
    ///
    /// // JSON request with custom headers
    /// var headers = new Dictionary&lt;string, string&gt; { { "X-Requested-With", "XMLHttpRequest" } };
    /// var json2 = await client.GetJsonAsync("https://api.example.com/ajax", headers, ct);
    /// </example>
    public async Task<JsonDocument> GetJsonAsync(string url, IDictionary<string, string>? headers, CancellationToken ct)
    {
        if (_useContextManager)
        {
            // Context Management Mode uses HttpClient for JSON requests (avoids wasting Context resources)
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");

            if (!string.IsNullOrWhiteSpace(_opt.UserAgent))
                req.Headers.TryAddWithoutValidation("User-Agent", _opt.UserAgent);
            if (!string.IsNullOrWhiteSpace(_opt.AcceptLanguage))
                req.Headers.TryAddWithoutValidation("Accept-Language", _opt.AcceptLanguage);

            if (headers != null)
            {
                foreach (var kv in headers)
                {
                    req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
            }

            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            await using var s = await resp.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(s, cancellationToken: ct);
        }
        else
        {
            // Simple Mode uses browser fetch
            var (ctx, page) = await NewContextAndPageAsync(ct);
            try
            {
                await page.GotoAsync("about:blank");
                var jsHeaders = headers is null ? "{}" :
                    "{" + string.Join(",", headers.Select(kv => $"{JsStr(kv.Key)}:{JsStr(kv.Value)}")) + "}";
                var script = $@"
                    async () => {{
                        const resp = await fetch({JsStr(url)}, {{ method: 'GET', headers: {jsHeaders} }});
                        const txt = await resp.text();
                        return txt;
                    }}";
                var text = await page.EvaluateAsync<string>(script);
                return JsonDocument.Parse(text);
            }
            finally
            {
                await SafeClose(page, ctx);
            }
        }

        static string JsStr(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    /// <summary>
    /// Opens a browser page for the specified URL with the configured client mode.
    /// </summary>
    /// <param name="url">Target URL to open</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Playwright page instance or null if operation fails</returns>
    /// <example>
    /// var page = await client.OpenPageAsync("https://dynamic-site.com", ct);
    /// if (page != null)
    /// {
    ///     await page.ClickAsync("button#load-more");
    ///     var elements = await page.QuerySelectorAllAsync(".result");
    ///     // Remember to close the page when done
    ///     await page.CloseAsync();
    /// }
    /// </example>
    public async Task<IPage?> OpenPageAsync(string url, CancellationToken ct)
    {
        if (_useContextManager)
        {
            return await OpenPageWithContextManagerAsync(url, ct);
        }
        else
        {
            var (ctx, page) = await NewContextAndPageAsync(ct);
            try
            {
                await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = _opt.GotoTimeoutMs });
                await WaitReadyAsync(page, ct);
                if (_antiBotHook is not null) await _antiBotHook(page, ct);
                return page;
            }
            catch
            {
                await SafeClose(page, ctx);
                throw;
            }
        }
    }

    // ========== Context Management Mode Methods ==========

    /// <summary>
    /// Opens a page using the context manager with automatic URL-based routing.
    /// Determines whether to use detail or search context based on URL patterns.
    /// </summary>
    /// <param name="url">Target URL</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Opened page or null if failed</returns>
    private async Task<IPage?> OpenPageWithContextManagerAsync(string url, CancellationToken ct)
    {
        if (_ctxMgr is null || _log is null) return null;

        // Simple URL-based judgment: detail page if contains "/watch", otherwise search
        bool forDetail = url.Contains("/watch", StringComparison.OrdinalIgnoreCase);
        return await OpenWithRetryAsync(url, forDetail, ct);
    }

    /// <summary>
    /// Opens a page with retry logic: primary attempt followed by slow retry on failure.
    /// Implements automatic context rotation when challenges are detected.
    /// </summary>
    /// <param name="url">Target URL</param>
    /// <param name="forDetail">Whether this is for detail page access</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Opened page or null if all attempts fail</returns>
    private async Task<IPage?> OpenWithRetryAsync(string url, bool forDetail, CancellationToken ct)
    {
        if (_ctxMgr is null || _log is null) return null;

        // First attempt: current context with standard timeout
        try
        {
            var page = await OpenOnceAsync(url, forDetail, primary: true, ct);
            return page;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex1)
        {
            _log.LogWarning(ex1, "[Nav] primary attempt failed: {Url}", url);

            // Second attempt: slow retry with extended timeout and context rotation flag
            try
            {
                var page = await OpenOnceAsync(url, forDetail, primary: false, ct);
                // Slow retry succeeded, notify ContextManager to rotate current Context
                _ctxMgr.FlagChallengeOnCurrent(forDetail: forDetail);
                _log.LogInformation("[Nav] slow-retry succeeded, flagged context for rotation");
                return page;
            }
            catch (Exception ex2)
            {
                _log.LogWarning(ex2, "[Nav] slow-retry failed: {Url}", url);
                return null;
            }
        }
    }

    /// <summary>
    /// Performs a single page opening attempt with configured timeout and anti-bot measures.
    /// Includes challenge detection and automatic context lifecycle management.
    /// </summary>
    /// <param name="url">Target URL</param>
    /// <param name="forDetail">Whether this is for detail page access</param>
    /// <param name="primary">Whether this is the primary attempt (affects timeout)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Successfully opened page</returns>
    /// <exception cref="InvalidOperationException">Thrown when challenge is detected</exception>
    private async Task<IPage> OpenOnceAsync(string url, bool forDetail, bool primary, CancellationToken ct)
    {
        if (_ctxMgr is null) throw new InvalidOperationException("Context manager is null");

        var ctx = await _ctxMgr.GetOrCreateContextAsync(forDetail);
        var page = await ctx.NewPageAsync();
        _ctxMgr.BumpOpenedPages(ctx, forDetail);

        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = primary ? 60_000 : _opt.SlowRetryGotoTimeoutMs
            });

            // Wait for key elements to ensure page is ready
            var readySelectors = _opt.ReadySelectors ?? new[] { "body" };
            foreach (var sel in readySelectors)
            {
                try
                {
                    await page.Locator(sel).First.WaitForAsync(new LocatorWaitForOptions
                    {
                        Timeout = primary ? 10_000 : _opt.SlowRetryWaitSelectorMs
                    });
                }
                catch { /* Tolerate individual selector timeouts */ }
            }

            // Execute anti-bot actions if configured
            if (_antiBotHook is not null)
            {
                try { await _antiBotHook(page, ct); } catch { /* Ignore anti-bot errors */ }
            }

            // Challenge detection (URL / DOM patterns)
            var html = await page.ContentAsync();
            if (LooksLikeChallenge(page.Url, html))
                throw new InvalidOperationException("Challenge detected.");

            return page;
        }
        catch
        {
            await SafeClosePage(page);
            throw;
        }
    }

    /// <summary>
    /// Detects if the current page appears to be a challenge or anti-bot page.
    /// Uses configured URL and DOM hints to identify challenge pages.
    /// </summary>
    /// <param name="curUrl">Current page URL</param>
    /// <param name="html">Page HTML content</param>
    /// <returns>True if challenge is detected, false otherwise</returns>
    private bool LooksLikeChallenge(string? curUrl, string? html)
    {
        if (!string.IsNullOrEmpty(curUrl))
        {
            foreach (var hint in _opt.ChallengeUrlHints)
                if (curUrl.Contains(hint, StringComparison.OrdinalIgnoreCase))
                    return true;
        }

        if (!string.IsNullOrEmpty(html))
        {
            foreach (var hint in _opt.ChallengeDomHints)
                if (html.Contains(hint, StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        return false;
    }

    // ========== Simple Mode Methods ==========

    /// <summary>
    /// Creates a new browser context and page for Simple Mode operations.
    /// Configures context with user agent, locale, viewport, and initialization scripts.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple containing the created context and page</returns>
    private async Task<(IBrowserContext Ctx, IPage Page)> NewContextAndPageAsync(CancellationToken ct)
    {
        var ctx = await _browser.NewContextAsync(new()
        {
            UserAgent = _opt.UserAgent,
            Locale = _opt.Locale,
            TimezoneId = _opt.TimezoneId,
            ViewportSize = new() { Width = _opt.ViewportWidth, Height = _opt.ViewportHeight }
        });
        await ctx.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { { "Accept-Language", _opt.AcceptLanguage } });

        if (!string.IsNullOrWhiteSpace(_opt.InitScriptPath) && File.Exists(_opt.InitScriptPath))
        {
            var script = await File.ReadAllTextAsync(_opt.InitScriptPath, ct);
            await ctx.AddInitScriptAsync(script);
        }

        var page = await ctx.NewPageAsync();
        return (ctx, page);
    }

    /// <summary>
    /// Waits for page readiness by checking for configured selector elements.
    /// Returns as soon as any configured selector is found.
    /// </summary>
    /// <param name="page">Page to check for readiness</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the async wait operation</returns>
    private async Task WaitReadyAsync(IPage page, CancellationToken ct)
    {
        foreach (var sel in _opt.ReadySelectors)
        {
            try
            {
                await page.Locator(sel).First.WaitForAsync(new LocatorWaitForOptions { Timeout = _opt.WaitSelectorTimeoutMs });
                return; // Any match is sufficient
            }
            catch { /* try next */ }
        }
    }

    // ========== Utility Methods ==========

    /// <summary>
    /// Converts ScrapeRuntimeOptions to PlaywrightClientOptions for internal use.
    /// Bridges the configuration between context manager and client options.
    /// </summary>
    /// <param name="options">Source runtime options</param>
    /// <returns>Converted client options</returns>
    private static PlaywrightClientOptions ConvertToPlaywrightClientOptions(ScrapeRuntimeOptions options)
    {
        return new PlaywrightClientOptions
        {
            UserAgent = options.UserAgent,
            Locale = options.Locale,
            TimezoneId = options.TimezoneId,
            AcceptLanguage = options.AcceptLanguage,
            ViewportWidth = options.ViewportWidth,
            ViewportHeight = options.ViewportHeight,
            InitScriptPath = options.StealthInitRelativePath,
            ReadySelectors = options.ReadySelectors ?? new[] { "body" },
            SlowRetryGotoTimeoutMs = options.SlowRetryGotoTimeoutMs,
            SlowRetryWaitSelectorMs = options.SlowRetryWaitSelectorMs,
            ChallengeUrlHints = options.ChallengeUrlHints,
            ChallengeDomHints = options.ChallengeDomHints,
            ContextTtlMinutes = options.ContextTtlMinutes,
            MaxPagesPerContext = options.MaxPagesPerContext,
            RotateOnChallengeDetected = options.RotateOnChallengeDetected,
            IsolationMode = (ContextIsolationMode)options.IsolationMode // Explicit cast
        };
    }

    /// <summary>
    /// Extracts the browser instance from a context manager using reflection.
    /// Required to access the underlying browser when using context management mode.
    /// </summary>
    /// <param name="contextManager">Context manager instance</param>
    /// <returns>Browser instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if browser cannot be accessed</exception>
    private static IBrowser GetBrowserFromContextManager(PlaywrightContextManager contextManager)
    {
        // Use reflection to get Browser instance
        var browserField = contextManager.GetType()
            .GetField("_browser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (browserField?.GetValue(contextManager) is IBrowser browser)
            return browser;

        throw new InvalidOperationException("Cannot access browser from context manager.");
    }

    /// <summary>
    /// Safely closes a page without affecting the context lifecycle.
    /// Used in Context Management Mode where context lifecycle is managed separately.
    /// </summary>
    /// <param name="page">Page to close (can be null)</param>
    /// <returns>Task representing the async close operation</returns>
    private static async Task SafeClosePage(IPage? page)
    {
        if (page is null) return;
        try { if (!page.IsClosed) await page.CloseAsync(); } catch { }
        // Don't close Context! ContextManager handles its lifecycle
    }

    /// <summary>
    /// Safely closes both page and context for Simple Mode operations.
    /// Ensures proper cleanup of resources when not using context management.
    /// </summary>
    /// <param name="page">Page to close</param>
    /// <param name="ctx">Context to close</param>
    /// <returns>Task representing the async close operation</returns>
    private static async Task SafeClose(IPage page, IBrowserContext ctx)
    {
        try { if (!page.IsClosed) await page.CloseAsync(); } catch { }
        try { await ctx.CloseAsync(); } catch { }
    }

    /// <summary>
    /// Disposes the client resources (no-op as actual cleanup is handled by the browser/context manager).
    /// </summary>
    /// <returns>Completed value task</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
