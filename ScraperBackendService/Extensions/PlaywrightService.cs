using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScraperBackendService.Core.Logging;

namespace ScraperBackendService.Extensions;

/// <summary>
/// Manages the Playwright IPlaywright instance and IBrowser lifecycle.
/// Registered as a singleton so the browser process is shared across the application.
/// </summary>
public class PlaywrightService : IAsyncDisposable, IDisposable
{
    private readonly ILogger<PlaywrightService> _logger;
    private readonly TaskCompletionSource<IBrowser> _browserTcs = new();
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _disposed = false;

    public PlaywrightService(ILogger<PlaywrightService> logger)
    {
        _logger = logger;
        _ = Task.Run(InitializeBrowserAsync);
    }

    public async Task<IBrowser> GetBrowserAsync()
    {
        return await _browserTcs.Task.ConfigureAwait(false);
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            _logger.LogDebug("Initializing Playwright browser...");
            _playwright = await Playwright.CreateAsync().ConfigureAwait(false);

            // Keep launch args minimal ¡ª extra flags make the browser easier to fingerprint.
            // Anti-automation is handled by the stealth script injected at context level.
            // Only remove --enable-automation from Playwright's defaults (it sets
            // navigator.webdriver=true and shows the automation infobar).
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = true,
                IgnoreDefaultArgs = new[] { "--enable-automation" }
            };

            _browser = await _playwright.Chromium.LaunchAsync(launchOptions).ConfigureAwait(false);

            _logger.LogAlways("PlaywrightService", "Playwright browser initialized successfully");
            _browserTcs.SetResult(_browser);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("PlaywrightService", "Failed to initialize Playwright browser", null, ex);
            _browserTcs.SetException(ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try
        {
            _logger.LogDebug("Disposing Playwright resources...");

            if (_browser != null && _browser.IsConnected)
            {
                await _browser.CloseAsync().ConfigureAwait(false);
                _logger.LogDebug("Browser closed successfully");
            }

            if (_playwright != null)
            {
                _playwright.Dispose();
                _logger.LogDebug("Playwright disposed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during Playwright disposal");
        }
        finally
        {
            _disposed = true;
        }
    }

    public void Dispose()
    {
        try
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during synchronous Playwright disposal");
        }
    }
}
