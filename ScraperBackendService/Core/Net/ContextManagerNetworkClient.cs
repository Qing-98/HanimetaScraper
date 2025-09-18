using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScraperBackendService.AntiCloudflare; // 你自己的命名空间：PlaywrightContextManager 在这里
using ScraperBackendService.Core.Abstractions;

namespace ScraperBackendService.Core.Net;

/// <summary>
/// 适配你现有的 AntiCloudflare/PlaywrightContextManager，
/// 实现 INetworkClient：OpenPageAsync / GetHtmlAsync / GetJsonAsync。
/// - 在 Net 层调用 antiBotHook（人类化动作）
/// - 挑战检测：命中 URL/DOM 关键字则判定为 Challenge
/// - 慢速重试：第二次使用更长超时，并标记当前 Context 以便轮换
/// - 资源释放：只关闭 Page，不关闭 Context（交由 ContextManager 维护）
/// </summary>
public sealed class ContextManagerNetworkClient : INetworkClient
{
    private readonly PlaywrightContextManager _ctxMgr;
    private readonly ILogger _log;
    private readonly Func<IPage, CancellationToken, Task>? _antiBotHook;

    /// <param name="ctxMgr">你项目里的 PlaywrightContextManager</param>
    /// <param name="log">日志</param>
    /// <param name="antiBotHook">反爬 Hook（可传你的人类化动作方法）</param>
    public ContextManagerNetworkClient(
        PlaywrightContextManager ctxMgr,
        ILogger log,
        Func<IPage, CancellationToken, Task>? antiBotHook = null)
    {
        _ctxMgr = ctxMgr;
        _log = log;
        _antiBotHook = antiBotHook;
    }

    // ========== INetworkClient ==========

    public async Task<IPage?> OpenPageAsync(string url, CancellationToken ct)
    {
        // 通过 URL 简单判断：详情页 forDetail=true，其余 false
        bool forDetail = url.Contains("/watch", StringComparison.OrdinalIgnoreCase);
        return await OpenWithRetryAsync(url, forDetail, ct);
    }

    public async Task<string> GetHtmlAsync(string url, CancellationToken ct)
    {
        var page = await OpenPageAsync(url, ct);
        if (page is null) return string.Empty;

        try
        {
            var html = await page.ContentAsync();
            return html;
        }
        finally
        {
            await SafeClosePage(page);
        }
    }

    // 简易 JSON GET（用于 DLsite 评分等）
    public async Task<JsonDocument> GetJsonAsync(string url, IDictionary<string, string>? headers, CancellationToken ct)
    {
        using var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");

        // 用 ContextManager 的 UA/语言
        var opt = _ctxMgr.Options;
        if (!string.IsNullOrWhiteSpace(opt.UserAgent))
            req.Headers.TryAddWithoutValidation("User-Agent", opt.UserAgent);
        if (!string.IsNullOrWhiteSpace(opt.AcceptLanguage))
            req.Headers.TryAddWithoutValidation("Accept-Language", opt.AcceptLanguage);

        if (headers != null)
        {
            foreach (var kv in headers)
            {
                // Referer 属于 request headers，AddWithoutValidation 更稳
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(s, cancellationToken: ct);
    }

    // ========== 内部：打开并带慢速重试 ==========

    private async Task<IPage?> OpenWithRetryAsync(string url, bool forDetail, CancellationToken ct)
    {
        // 第一次尝试：当前 Context
        try
        {
            var page = await OpenOnceAsync(url, forDetail, primary: true, ct);
            return page;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex1)
        {
            _log.LogWarning(ex1, "[Nav] primary attempt failed: {Url}", url);

            // 第二次尝试：慢速重试（更长超时 & 标记轮换）
            try
            {
                var page = await OpenOnceAsync(url, forDetail, primary: false, ct);
                // 慢速重试成功，告知 ContextManager 轮换当前 Context
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

    private async Task<IPage> OpenOnceAsync(string url, bool forDetail, bool primary, CancellationToken ct)
    {
        var ctx = await _ctxMgr.GetOrCreateContextAsync(forDetail);
        var page = await ctx.NewPageAsync();
        _ctxMgr.BumpOpenedPages(ctx, forDetail);

        var opt = _ctxMgr.Options;
        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = primary ? 60_000 : opt.SlowRetryGotoTimeoutMs
            });

            // 等关键元素（你可以在 Options 里自定义；没有就等 body）
            var readySelectors = opt.ReadySelectors ?? new[] { "body" };
            foreach (var sel in readySelectors)
            {
                try
                {
                    await page.Locator(sel).First.WaitForAsync(new LocatorWaitForOptions
                    {
                        Timeout = primary ? 10_000 : opt.SlowRetryWaitSelectorMs
                    });
                }
                catch { /* 容忍个别 selector 超时 */ }
            }

            // 反爬动作
            if (_antiBotHook is not null)
            {
                try { await _antiBotHook(page, ct); } catch { /* 忽略 */ }
            }

            // 挑战检测（URL / DOM）
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

    private bool LooksLikeChallenge(string? curUrl, string? html)
    {
        var opt = _ctxMgr.Options;

        if (!string.IsNullOrEmpty(curUrl))
        {
            foreach (var hint in opt.ChallengeUrlHints)
                if (curUrl.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
        }

        if (!string.IsNullOrEmpty(html))
        {
            foreach (var hint in opt.ChallengeDomHints)
                if (html.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
        }
        return false;
    }

    private static async Task SafeClosePage(IPage? page)
    {
        if (page is null) return;
        try { if (!page.IsClosed) await page.CloseAsync(); } catch { }
        // 不关闭 Context！ContextManager 负责它的生命周期
    }
}
