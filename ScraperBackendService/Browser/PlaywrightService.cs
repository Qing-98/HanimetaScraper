using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using ScraperBackendService.Configuration;
using ScraperBackendService.Core.Logging;

namespace ScraperBackendService.Browser;

/// <summary>
/// Manages the Playwright IPlaywright instance and IBrowser lifecycle.
/// Registered as a singleton so the browser process is shared across the application.
/// </summary>
public class PlaywrightService : IAsyncDisposable, IDisposable
{
    private readonly ILogger<PlaywrightService> _logger;
    private readonly ServiceConfiguration _config;
    private readonly Lazy<Task<IBrowser>> _browserLazy;
    private IPlaywright? _playwright;
    private bool _disposed = false;

    public PlaywrightService(ILogger<PlaywrightService> logger, IOptions<ServiceConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
        _browserLazy = new Lazy<Task<IBrowser>>(InitializeBrowserAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<IBrowser> GetBrowserAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _browserLazy.Value.ConfigureAwait(false);
    }

    private async Task<IBrowser> InitializeBrowserAsync()
    {
        _logger.LogDebug("Initializing Playwright browser...");
        _playwright = await Playwright.CreateAsync().ConfigureAwait(false);

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = true,
            IgnoreDefaultArgs = new[] { "--enable-automation" }
        };

        IBrowser browser;
        if (_config.PreferSystemChromeForHeadless)
        {
            try
            {
                launchOptions.Channel = "chrome";
                browser = await _playwright.Chromium.LaunchAsync(launchOptions).ConfigureAwait(false);
                _logger.LogSuccess("PlaywrightService", "Headless browser launched using system Chrome channel");
                return browser;
            }
            catch (PlaywrightException ex)
            {
                _logger.LogWarning(ex, "System Chrome channel launch failed, falling back to bundled Chromium");
                launchOptions.Channel = null;
            }
        }

        browser = await _playwright.Chromium.LaunchAsync(launchOptions).ConfigureAwait(false);
        _logger.LogAlways("PlaywrightService", "Playwright browser initialized successfully");
        return browser;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try
        {
            _logger.LogDebug("Disposing Playwright resources...");

            if (_browserLazy.IsValueCreated)
            {
                try
                {
                    var browser = await _browserLazy.Value.ConfigureAwait(false);
                    if (browser.IsConnected)
                    {
                        await browser.CloseAsync().ConfigureAwait(false);
                        _logger.LogDebug("Browser closed successfully");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while closing browser instance");
                }
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
