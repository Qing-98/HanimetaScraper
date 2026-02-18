using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScraperBackendService.Core.Logging;
using ScraperBackendService.Core.Net;
using System.Diagnostics;
using System.IO;

namespace ScraperBackendService.AntiCloudflare;

/// <summary>
/// 处理需要人工干预的挑战页面
/// </summary>
public class ManualChallengeHandler
{
    private readonly ILogger _logger;
    private readonly ChallengeDetector _challengeDetector;

    public ManualChallengeHandler(ILogger logger, ChallengeDetector challengeDetector)
    {
        _logger = logger;
        _challengeDetector = challengeDetector;
    }

    /// <summary>
    /// 弹出浏览器窗口让用户手动完成挑战
    /// </summary>
    /// <param name="url">目标 URL</param>
    /// <param name="options">运行时选项</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>成功时返回包含 cookies 和 UserAgent 的结果对象,失败返回 null</returns>
    public async Task<ManualChallengeResult?> HandleManualChallengeAsync(
        string url,
        PlaywrightClientOptions options,
        CancellationToken ct)
    {
        IPlaywright? playwright = null;
        IBrowserContext? visibleContext = null;
        IPage? visiblePage = null;

        try
        {
            _logger.LogWarning("ManualChallenge", "需要人工验证,正在启动独立浏览器窗口...", url);

            playwright = await Playwright.CreateAsync();

            // 使用跨会话持久化的 Chrome 配置文件，让浏览器积累历史/cookies/扩展
            // 空白的临时配置文件是强烈的自动化信号；积累过历史记录的配置文件看起来才像真实用户
            var profileDir = GetChallengeProfileDir();
            _logger.LogDebug("ManualChallenge", $"Chrome 配置文件: {profileDir}");
            visibleContext = await LaunchPersistentContextAsync(playwright, profileDir);

            // 注入反自动化检测脚本
            await InjectStealthScriptAsync(visibleContext, options);

            // 移除 Accept-Language 覆盖,使用浏览器默认值 (通常与 OS 语言一致)
            /*
            await visibleContext.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
            {
                { "Accept-Language", options.AcceptLanguage }
            });
            */

            // PersistentContext 可能已有默认页面
            visiblePage = visibleContext.Pages.Count > 0
                ? visibleContext.Pages[0]
                : await visibleContext.NewPageAsync();

            // 导航到挑战页面
            _logger.LogInformation("[ManualChallenge] 正在加载页面... {Url}", url);
            await visiblePage.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

            // 显示提示消息
            _logger.LogWarning("ManualChallenge", 
                $"⚠️ 请在浏览器窗口中完成验证\n" +
                $"   完成后,页面会自动检测并继续\n" +
                $"   最长等待时间: {options.ManualChallengeTimeoutSeconds} 秒",
                url);

            // 显示控制台消息给用户
            await ShowUserInstructionAsync(visiblePage);

            // 等待用户完成挑战
            var success = await WaitForChallengeCompletionAsync(
                visiblePage, 
                url, 
                TimeSpan.FromSeconds(options.ManualChallengeTimeoutSeconds),
                ct);

            if (!success)
            {
                _logger.LogFailure("ManualChallenge", "验证失败或超时", url);
                return null;
            }

            _logger.LogSuccess("ManualChallenge", $"Verification Success ({url})");

            // 提取所有 cookies
            var cookies = await visibleContext.CookiesAsync();
            
            // 获取当前实际使用的 UserAgent
            var userAgent = await visiblePage.EvaluateAsync<string>("() => navigator.userAgent");
            
            _logger.LogSuccess("ManualChallenge", $"Extracted cookies - {url}", cookies.Count);
            _logger.LogDebug("ManualChallenge", $"UA: {userAgent}");

            return new ManualChallengeResult(cookies, userAgent);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("ManualChallenge", "处理过程中发生错误", url, ex);
            return null;
        }
        finally
        {
            // 不删除配置文件目录——保留历史记录和 cookies 供下次使用
            if (visibleContext != null) try { await visibleContext.CloseAsync(); } catch { }
            playwright?.Dispose();
        }
    }

    /// <summary>
    /// 启动持久化上下文,优先使用系统 Chrome,失败则回退到内置 Chromium
    /// </summary>
    private async Task<IBrowserContext> LaunchPersistentContextAsync(
        IPlaywright playwright,
        string userDataDir)
    {
        // Use IgnoreDefaultArgs (NOT IgnoreAllDefaultArgs) to surgically remove only the
        // fingerprinting flags while keeping Playwright's CDP control infrastructure intact.
        // IgnoreAllDefaultArgs=true also strips --remote-debugging-pipe and other control
        // args, which causes Chrome to open but be uncontrollable (shows default homepage).
        var launchOptions = new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = false,
            IgnoreDefaultArgs =
            [
                "--enable-automation",  // Removes navigator.webdriver=true and the automation infobar
                "--disable-extensions", // Allows real Chrome extensions to load (more realistic fingerprint)
                "--no-sandbox",         // Prevents "unsupported flag" warning bar (safe on Windows; avoids warning on Linux)
            ],
            ViewportSize = null,
        };

        // System Chrome has a genuine TLS/JA3 fingerprint that Cloudflare recognises as real.
        // Playwright's bundled Chromium has a distinct fingerprint that Cloudflare flags.
        try
        {
            launchOptions.Channel = "chrome";
            var ctx = await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, launchOptions);
            _logger.LogInformation("ManualChallenge", "使用系统 Chrome 启动成功");
            return ctx;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("ManualChallenge", "系统 Chrome 不可用, 回退到内置 Chromium", null, ex);
            launchOptions.Channel = null;
            return await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, launchOptions);
        }
    }

    /// <summary>
    /// 返回跨会话持久化的 Chrome 配置文件目录，首次访问时自动创建
    /// </summary>
    private static string GetChallengeProfileDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HanimetaScraper", "ChallengeProfile");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 注入反自动化检测脚本到浏览器上下文
    /// </summary>
    private async Task InjectStealthScriptAsync(IBrowserContext context, PlaywrightClientOptions options)
    {
        try
        {
            var scriptPath = options.InitScriptPath;
            if (!string.IsNullOrWhiteSpace(scriptPath))
            {
                var path = Path.IsPathRooted(scriptPath)
                    ? scriptPath
                    : Path.Combine(AppContext.BaseDirectory, scriptPath);

                if (File.Exists(path))
                {
                    var script = await File.ReadAllTextAsync(path);
                    await context.AddInitScriptAsync(script);
                    _logger.LogDebug("ManualChallenge", "已注入反自动化检测脚本");
                    return;
                }
            }

            // 如果找不到脚本文件,使用内联的关键反检测代码
            await context.AddInitScriptAsync(@"
                (() => {
                    // 1. 彻底移除 webdriver 属性 (防止 flag 漏网)
                    delete navigator.webdriver;
                    Object.defineProperty(navigator, 'webdriver', {
                        get: () => undefined
                    });

                    // 2. 模拟 Chrome 插件列表
                    if (navigator.plugins.length === 0) {
                        Object.defineProperty(navigator, 'plugins', {
                            get: () => [1, 2, 3, 4, 5],
                        });
                    }

                    // 3. 模拟 Chrome 语言
                    if (!navigator.languages || navigator.languages.length === 0) {
                        Object.defineProperty(navigator, 'languages', {
                            get: () => ['zh-CN', 'zh', 'en'],
                        });
                    }
                    
                    // 4. 模拟 window.chrome
                    if (!window.chrome) {
                        const chrome = {
                            runtime: {},
                            loadTimes: function() {},
                            csi: function() {},
                            app: {}
                        };
                        Object.defineProperty(window, 'chrome', { get: () => chrome });
                    }
                    
                    // 5. 欺骗通知权限
                    if (window.navigator.permissions) {
                        const originalQuery = window.navigator.permissions.query;
                        window.navigator.permissions.query = (parameters) => (
                            parameters.name === 'notifications' ?
                                Promise.resolve({ state: Notification.permission }) :
                                originalQuery(parameters)
                        );
                    }
                    
                    // 6. 模拟网络连接信息 (NetworkInformation)
                    if (!navigator.connection) {
                        Object.defineProperty(navigator, 'connection', {
                            get: () => ({
                                rtt: 50,
                                downlink: 10,
                                effectiveType: '4g',
                                saveData: false
                            })
                        });
                    }
                })();
            ");
        }
        catch (Exception ex)
        {
            _logger.LogDebug("ManualChallenge", "注入反检测脚本失败,继续执行", null, ex);
        }
    }

    /// <summary>
    /// 在页面上显示用户指引
    /// </summary>
    private async Task ShowUserInstructionAsync(IPage page)
    {
        try
        {
            await page.EvaluateAsync(@"
                () => {
                    const div = document.createElement('div');
                    div.id = 'manual-challenge-instruction';
                    div.style.cssText = `
                        position: fixed;
                        top: 20px;
                        left: 50%;
                        transform: translateX(-50%);
                        z-index: 999999;
                        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                        color: white;
                        padding: 20px 30px;
                        border-radius: 12px;
                        box-shadow: 0 10px 40px rgba(0,0,0,0.3);
                        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
                        font-size: 16px;
                        text-align: center;
                        animation: slideDown 0.5s ease-out;
                    `;
                    div.innerHTML = `
                        <div style='font-size: 24px; margin-bottom: 10px;'>🔐 人工验证</div>
                        <div style='font-size: 14px; opacity: 0.9;'>
                            请完成下方的验证挑战<br>
                            完成后页面将自动继续
                        </div>
                    `;
                    
                    const style = document.createElement('style');
                    style.textContent = `
                        @keyframes slideDown {
                            from { transform: translate(-50%, -100%); opacity: 0; }
                            to { transform: translate(-50%, 0); opacity: 1; }
                        }
                    `;
                    document.head.appendChild(style);
                    document.body.appendChild(div);
                }
            ");
        }
        catch (Exception ex)
        {
            _logger.LogDebug("ManualChallenge", "无法显示用户指引", null, ex);
        }
    }

    /// <summary>
    /// 等待用户完成挑战
    /// </summary>
    private async Task<bool> WaitForChallengeCompletionAsync(
        IPage page, 
        string originalUrl,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var checkInterval = TimeSpan.FromSeconds(2);

        while (stopwatch.Elapsed < timeout && !ct.IsCancellationRequested)
        {
            try
            {
                if (page.IsClosed)
                {
                    _logger.LogWarning("ManualChallenge", "用户手动关闭了窗口");
                    return false;
                }

                await Task.Delay(checkInterval, ct);

                var currentUrl = page.Url;
                // 使用 EvaluateAsync 获取内容更稳定，且不容易像 ContentAsync 那样被某些反爬检测
                var html = await page.EvaluateAsync<string>("() => document.documentElement.outerHTML");

                // 检查是否还是挑战页
                if (!_challengeDetector.IsCloudflareChallengePage(html, currentUrl))
                {
                    // 验证页面有有效内容
                    var hasContent = await _challengeDetector.HasValidContentAsync(page);
                    if (hasContent)
                    {
                        // 额外稳定性等待: 确认挑战不会再次出现
                        await Task.Delay(3000, ct);
                        
                        var verifyHtml = await page.ContentAsync();
                        var verifyUrl = page.Url;
                        if (_challengeDetector.IsCloudflareChallengePage(verifyHtml, verifyUrl))
                        {
                            _logger.LogDebug("ManualChallenge", "挑战在短暂消失后又重新出现,继续等待...");
                            continue;
                        }
                        
                        _logger.LogSuccess("ManualChallenge", $"Challenge Solved (Took {stopwatch.Elapsed.TotalSeconds:F1}s)");
                        
                        // 移除提示框
                        await RemoveInstructionAsync(page);
                        
                        // 额外等待确保所有 cookies 都设置完成
                        await Task.Delay(2000, ct);
                        
                        return true;
                    }
                }

                // 每10秒输出一次进度
                if (stopwatch.Elapsed.TotalSeconds % 10 < 2)
                {
                    var remaining = (timeout - stopwatch.Elapsed).TotalSeconds;
                    _logger.LogDebug("ManualChallenge", 
                        $"等待中... (剩余时间: {remaining:F0}秒)");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("ManualChallenge", "检查挑战状态时出错", null, ex);
            }
        }

        if (ct.IsCancellationRequested)
        {
            _logger.LogWarning("ManualChallenge", "操作已取消", originalUrl);
        }
        else
        {
            _logger.LogFailure("ManualChallenge", $"验证超时 ({timeout.TotalSeconds}秒)", originalUrl);
        }

        return false;
    }

    /// <summary>
    /// 移除指引提示框
    /// </summary>
    private async Task RemoveInstructionAsync(IPage page)
    {
        try
        {
            await page.EvaluateAsync(@"
                () => {
                    const div = document.getElementById('manual-challenge-instruction');
                    if (div) {
                        div.style.animation = 'slideUp 0.5s ease-out';
                        setTimeout(() => div.remove(), 500);
                    }
                    
                    const style = document.createElement('style');
                    style.textContent = `
                        @keyframes slideUp {
                            from { transform: translate(-50%, 0); opacity: 1; }
                            to { transform: translate(-50%, -100%); opacity: 0; }
                        }
                    `;
                    document.head.appendChild(style);
                }
            ");
        }
        catch { }
    }
}

/// <summary>
/// 手动挑战结果
/// </summary>
/// <param name="Cookies">获取到的 Cookies</param>
/// <param name="UserAgent">通过挑战时使用的 UserAgent</param>
public record ManualChallengeResult(IReadOnlyList<BrowserContextCookiesResult> Cookies, string UserAgent);
