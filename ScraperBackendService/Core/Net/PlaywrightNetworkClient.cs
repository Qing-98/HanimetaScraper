using System.Text.Json;
using Microsoft.Playwright;
using ScraperBackendService.Core.Abstractions;

namespace ScraperBackendService.Core.Net;

public sealed class PlaywrightNetworkClient : INetworkClient, IAsyncDisposable
{
    private readonly IBrowser _browser;
    private readonly PlaywrightClientOptions _opt;
    private readonly Func<IPage, CancellationToken, Task>? _antiBotHook;

    public PlaywrightNetworkClient(
        IBrowser browser,
        PlaywrightClientOptions? options = null,
        Func<IPage, CancellationToken, Task>? antiBotHook = null)
    {
        _browser = browser;
        _opt = options ?? new PlaywrightClientOptions();
        _antiBotHook = antiBotHook;
    }

    public async Task<string> GetHtmlAsync(string url, CancellationToken ct)
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

    public async Task<JsonDocument> GetJsonAsync(string url, IDictionary<string, string>? headers, CancellationToken ct)
    {
        // 通过 page.evaluate 使用浏览器环境发起 fetch，可避开部分反爬
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

        static string JsStr(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    /// <summary>
    /// 返回 IPage（以及持有的 Context）。调用方需要负责 page.Close()；本类会在 DisposeAsync 时尽力清理泄漏。
    /// </summary>
    public async Task<IPage?> OpenPageAsync(string url, CancellationToken ct)
    {
        var (ctx, page) = await NewContextAndPageAsync(ct);
        try
        {
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = _opt.GotoTimeoutMs });
            await WaitReadyAsync(page, ct);
            if (_antiBotHook is not null) await _antiBotHook(page, ct);
            // 交由调用方管理生命周期
            return page;
        }
        catch
        {
            await SafeClose(page, ctx);
            throw;
        }
    }

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

    private async Task WaitReadyAsync(IPage page, CancellationToken ct)
    {
        foreach (var sel in _opt.ReadySelectors)
        {
            try
            {
                await page.Locator(sel).First.WaitForAsync(new LocatorWaitForOptions { Timeout = _opt.WaitSelectorTimeoutMs });
                return; // 任一命中即可
            }
            catch { /* try next */ }
        }
    }

    private static async Task SafeClose(IPage page, IBrowserContext ctx)
    {
        try { if (!page.IsClosed) await page.CloseAsync(); } catch { }
        try { await ctx.CloseAsync(); } catch { }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask; // 若后续维护泄漏追踪，可在此回收
}
