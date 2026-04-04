using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScraperBackendService.Browser;
using ScraperBackendService.Core.Logging;
using ScraperBackendService.Core.Net;
using System.Diagnostics;
using System.IO;

namespace ScraperBackendService.Browser;

/// <summary>
/// Handles challenge pages that require manual intervention
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
    /// Opens a browser window for the user to manually complete the challenge
    /// </summary>
    /// <param name="url">Target URL</param>
    /// <param name="options">Runtime options</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Returns a result object containing cookies and UserAgent on success, null on failure</returns>
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
            _logger.LogWarning("ManualChallenge", "Manual verification required, launching standalone browser window...", url);

            playwright = await Playwright.CreateAsync();

            // Use a cross-session persistent Chrome profile to let the browser accumulate history/cookies/extensions
            // A blank temporary profile is a strong automation signal; a profile with accumulated history looks like a real user
            var profileDir = GetChallengeProfileDir();
            _logger.LogDebug("ManualChallenge", $"Chrome profile: {profileDir}");
            visibleContext = await LaunchPersistentContextAsync(playwright, profileDir);

            // Inject anti-automation detection script
            await InjectStealthScriptAsync(visibleContext, options);

            // Remove Accept-Language override, use browser default (usually matches OS language)
            /*
            await visibleContext.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
            {
                { "Accept-Language", options.AcceptLanguage }
            });
            */

            // PersistentContext may already have a default page
            visiblePage = visibleContext.Pages.Count > 0
                ? visibleContext.Pages[0]
                : await visibleContext.NewPageAsync();

            // Navigate to the challenge page
            _logger.LogDebug("ManualChallenge", "Loading page...", url);
            await visiblePage.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

            // Display hint message
            _logger.LogWarning("ManualChallenge", 
                $"⚠️ Please complete the verification in the browser window\n" +
                $"   The page will automatically detect and continue after completion\n" +
                $"   Maximum wait time: {options.ManualChallengeTimeoutSeconds} seconds",
                url);

            // Show instruction overlay to user
            await ShowUserInstructionAsync(visiblePage);

            // Wait for the user to complete the challenge
            var success = await WaitForChallengeCompletionAsync(
                visiblePage, 
                url, 
                TimeSpan.FromSeconds(options.ManualChallengeTimeoutSeconds),
                ct);

            if (!success)
            {
                _logger.LogFailure("ManualChallenge", "Verification failed or timed out", url);
                return null;
            }

            _logger.LogSuccess("ManualChallenge", $"Verification Success ({url})");

            // Extract all cookies
            var cookies = await visibleContext.CookiesAsync();
            
            // Get the currently used UserAgent
            var userAgent = await visiblePage.EvaluateAsync<string>("() => navigator.userAgent");
            
            _logger.LogSuccess("ManualChallenge", $"Extracted cookies - {url}", cookies.Count);
            _logger.LogDebug("ManualChallenge", $"UA: {userAgent}");

            return new ManualChallengeResult(cookies, userAgent);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("ManualChallenge", "An error occurred during processing", url, ex);
            return null;
        }
        finally
        {
            // Do not delete the profile directory — preserve history and cookies for next use
            if (visibleContext != null) try { await visibleContext.CloseAsync(); } catch { }
            playwright?.Dispose();
        }
    }

    /// <summary>
    /// Launches a persistent context, preferring system Chrome and falling back to bundled Chromium on failure
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
            _logger.LogSuccess("ManualChallenge", "Successfully launched using system Chrome");
            return ctx;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("ManualChallenge", "System Chrome unavailable, falling back to bundled Chromium", null, ex);
            launchOptions.Channel = null;
            return await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, launchOptions);
        }
    }

    /// <summary>
    /// Returns the cross-session persistent Chrome profile directory, creating it on first access
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
    /// Injects anti-automation detection scripts into the browser context
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
                    _logger.LogDebug("ManualChallenge", "Anti-automation detection script injected");
                    return;
                }
            }

            // If the script file is not found, use inline critical anti-detection code
            await context.AddInitScriptAsync(@"
                (() => {
                    // 1. Completely remove the webdriver property (prevent flag leakage)
                    delete navigator.webdriver;
                    Object.defineProperty(navigator, 'webdriver', {
                        get: () => undefined
                    });

                    // 2. Simulate Chrome plugin list
                    if (navigator.plugins.length === 0) {
                        Object.defineProperty(navigator, 'plugins', {
                            get: () => [1, 2, 3, 4, 5],
                        });
                    }

                    // 3. Simulate Chrome language
                    if (!navigator.languages || navigator.languages.length === 0) {
                        Object.defineProperty(navigator, 'languages', {
                            get: () => ['zh-CN', 'zh', 'en'],
                        });
                    }
                    
                    // 4. Simulate window.chrome
                    if (!window.chrome) {
                        const chrome = {
                            runtime: {},
                            loadTimes: function() {},
                            csi: function() {},
                            app: {}
                        };
                        Object.defineProperty(window, 'chrome', { get: () => chrome });
                    }
                    
                    // 5. Spoof notification permissions
                    if (window.navigator.permissions) {
                        const originalQuery = window.navigator.permissions.query;
                        window.navigator.permissions.query = (parameters) => (
                            parameters.name === 'notifications' ?
                                Promise.resolve({ state: Notification.permission }) :
                                originalQuery(parameters)
                        );
                    }
                    
                    // 6. Simulate network connection info (NetworkInformation)
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
            _logger.LogDebug("ManualChallenge", "Failed to inject anti-detection script, continuing", null, ex);
        }
    }

    /// <summary>
    /// Displays user instructions on the page
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
                        <div style='font-size: 24px; margin-bottom: 10px;'>🔐 Manual Verification</div>
                        <div style='font-size: 14px; opacity: 0.9;'>
                            Please complete the verification challenge below<br>
                            The page will continue automatically after completion
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
            _logger.LogDebug("ManualChallenge", "Unable to display user instructions", null, ex);
        }
    }

    /// <summary>
    /// Waits for the user to complete the challenge
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
                    _logger.LogWarning("ManualChallenge", "User manually closed the window");
                    return false;
                }

                await Task.Delay(checkInterval, ct);

                var currentUrl = page.Url;
                // Using EvaluateAsync to read page content is more stable and less likely to trigger anti-bot detection than ContentAsync in some cases.
                var html = await page.EvaluateAsync<string>("() => document.documentElement.outerHTML");

                // Check whether this is still a challenge page
                if (!_challengeDetector.IsCloudflareChallengePage(html, currentUrl))
                {
                    // Validate that the page contains real content
                    var hasContent = await _challengeDetector.HasValidContentAsync(page);
                    if (hasContent)
                    {
                        // Extra stabilization wait to ensure the challenge does not immediately reappear
                        await Task.Delay(3000, ct);
                        
                        var verifyHtml = await page.ContentAsync();
                        var verifyUrl = page.Url;
                        if (_challengeDetector.IsCloudflareChallengePage(verifyHtml, verifyUrl))
                        {
                            _logger.LogDebug("ManualChallenge", "Challenge reappeared after briefly disappearing, continuing to wait...");
                            continue;
                        }

                        await LogManualDiagnosticsAsync(page, verifyHtml, originalUrl, "manual-solved");
                        
                        _logger.LogSuccess("ManualChallenge", $"Challenge Solved (Took {stopwatch.Elapsed.TotalSeconds:F1}s)");
                        
                        // Remove the instruction overlay
                        await RemoveInstructionAsync(page);
                        
                        // Extra wait to ensure all cookies are fully set
                        await Task.Delay(2000, ct);
                        
                        return true;
                    }
                }

                // Log progress every ~10 seconds
                if (stopwatch.Elapsed.TotalSeconds % 10 < 2)
                {
                    var remaining = (timeout - stopwatch.Elapsed).TotalSeconds;
                    _logger.LogDebug("ManualChallenge", 
                        $"Waiting... (remaining: {remaining:F0}s)");
                    await LogManualDiagnosticsAsync(page, html, originalUrl, "manual-waiting");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("ManualChallenge", "Error while checking challenge status", null, ex);
            }
        }

        if (ct.IsCancellationRequested)
        {
            _logger.LogWarning("ManualChallenge", "Operation cancelled", originalUrl);
        }
        else
        {
            _logger.LogFailure("ManualChallenge", $"Verification timed out ({timeout.TotalSeconds}s)", originalUrl);
        }

        return false;
    }

    private async Task LogManualDiagnosticsAsync(IPage page, string html, string requestUrl, string stage)
    {
        try
        {
            var analysis = _challengeDetector.AnalyzeChallengePage(html);
            var reasons = analysis.Reasons.Count == 0 ? "none" : string.Join(",", analysis.Reasons);

            var title = string.Empty;
            try { title = await page.TitleAsync(); } catch { }

            var userAgent = string.Empty;
            try { userAgent = await page.EvaluateAsync<string>("() => navigator.userAgent"); } catch { }

            bool hasCfClearance = false;
            try
            {
                var cookies = await page.Context.CookiesAsync([requestUrl]);
                hasCfClearance = cookies.Any(c => string.Equals(c.Name, "cf_clearance", StringComparison.OrdinalIgnoreCase));
            }
            catch { }

            _logger.LogDebug("ManualChallenge", $"[Diagnostics:{stage}] request={requestUrl} pageUrl={page.Url} title={title} ua={userAgent} cf_clearance={hasCfClearance} detectorReasons={reasons}");
        }
        catch (Exception ex)
        {
            _logger.LogDebug("ManualChallenge", "Failed to collect manual challenge diagnostics", requestUrl, ex);
        }
    }

    /// <summary>
    /// Removes the instruction overlay
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
/// Manual challenge result
/// </summary>
/// <param name="Cookies">The cookies obtained</param>
/// <param name="UserAgent">The UserAgent used when passing the challenge</param>
public record ManualChallengeResult(IReadOnlyList<BrowserContextCookiesResult> Cookies, string UserAgent);
