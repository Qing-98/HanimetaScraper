using Microsoft.Playwright;
using ScraperBackendService.Configuration;
using ScraperBackendService.Core.Net;
using ScraperBackendService.Providers.DLsite;
using ScraperBackendService.Providers.Hanime;
using ScraperBackendService.AntiCloudflare;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;
using ScraperBackendService.Core.Logging;
using Microsoft.Extensions.Hosting;

namespace ScraperBackendService.Extensions;

/// <summary>
/// Service registration extension methods for dependency injection configuration.
/// Provides streamlined setup of scraping services, network clients, and content providers.
/// </summary>
/// <example>
/// Usage in Program.cs:
/// var builder = WebApplication.CreateBuilder(args);
/// var serviceConfig = new ServiceConfiguration();
/// builder.Configuration.GetSection(ServiceConfiguration.SectionName).Bind(serviceConfig);
///
/// // Register all scraping services
/// builder.Services.AddScrapingServices(serviceConfig);
///
/// var app = builder.Build();
///
/// // Services are now available for injection:
/// // - HanimeProvider (with PlaywrightNetworkClient)
/// // - DlsiteProvider (with HttpNetworkClient)
/// // - IBrowser (Playwright Chromium instance)
/// // - ServiceConfiguration (bound to config)
/// </example>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all scraping-related services including configuration, network clients, and content providers.
    /// Sets up dependency injection for the complete scraping pipeline.
    /// </summary>
    /// <param name="services">Service collection to register services with</param>
    /// <param name="config">Service configuration containing operational parameters</param>
    /// <returns>Service collection for method chaining</returns>
    /// <example>
    /// var config = new ServiceConfiguration
    /// {
    ///     Port = 8585,
    ///     Host = "0.0.0.0",
    ///     AuthToken = "secret-token",
    ///     MaxConcurrentRequests = 10,
    ///     RequestTimeoutSeconds = 60,
    ///     EnableDetailedLogging = true
    /// };
    ///
    /// services.AddScrapingServices(config);
    ///
    /// // Registered services:
    /// // - ServiceConfiguration (configured options)
    /// // - IBrowser (Playwright Chromium browser)
    /// // - PlaywrightNetworkClient (for JavaScript-heavy sites)
    /// // - HttpNetworkClient (for static content)
    /// // - HanimeProvider (uses Playwright for dynamic content)
    /// // - DlsiteProvider (uses HTTP for efficient scraping)
    /// </example>
    public static IServiceCollection AddScrapingServices(this IServiceCollection services, ServiceConfiguration config)
    {
        // Register configuration options for dependency injection
        services.Configure<ServiceConfiguration>(opt =>
        {
            opt.Port = config.Port;
            opt.Host = config.Host;
            opt.AuthToken = config.AuthToken;
            opt.TokenHeaderName = config.TokenHeaderName;
            opt.RequestTimeoutSeconds = config.RequestTimeoutSeconds;
            opt.HanimeMaxConcurrentRequests = config.HanimeMaxConcurrentRequests;
            opt.DlsiteMaxConcurrentRequests = config.DlsiteMaxConcurrentRequests;
            opt.EnableAggressiveMemoryOptimization = config.EnableAggressiveMemoryOptimization;
        });

        // Register provider-specific concurrency limiters
        var hanimeLimit = config.HanimeMaxConcurrentRequests;
        var dlsiteLimit = config.DlsiteMaxConcurrentRequests;

        // Use typed concurrency limiter classes
        services.AddSingleton(new ScraperBackendService.Core.Concurrency.HanimeConcurrencyLimiter(hanimeLimit));
        services.AddSingleton(new ScraperBackendService.Core.Concurrency.DlsiteConcurrencyLimiter(dlsiteLimit));

        // Register PlaywrightService as singleton to manage browser lifecycle
        services.AddSingleton<PlaywrightService>();

        // Register Playwright browser as singleton with proper disposal
        services.AddSingleton<IBrowser>(sp =>
        {
            var playwrightService = sp.GetRequiredService<PlaywrightService>();
            return playwrightService.GetBrowserAsync().GetAwaiter().GetResult();
        });

        // Register PlaywrightContextManager as singleton using the browser instance
        services.AddSingleton<PlaywrightContextManager>(sp =>
        {
            var browser = sp.GetRequiredService<IBrowser>();
            var logger = sp.GetRequiredService<ILogger<PlaywrightContextManager>>();
            var options = new ScrapeRuntimeOptions();
            
            return new PlaywrightContextManager(browser, logger, options);
        });

        // Register PlaywrightNetworkClient using context manager (recommended)
        services.AddScoped<PlaywrightNetworkClient>(sp =>
        {
            var ctxMgr = sp.GetRequiredService<PlaywrightContextManager>();
            var logger = sp.GetRequiredService<ILogger<PlaywrightNetworkClient>>();
            return new PlaywrightNetworkClient(ctxMgr, logger);
        });

        // Register HttpNetworkClient via IHttpClientFactory to ensure pooling
        services.AddHttpClient<HttpNetworkClient>();

        // Register content providers
        services.AddScoped<HanimeProvider>(sp =>
        {
            var playwrightClient = sp.GetRequiredService<PlaywrightNetworkClient>();
            var logger = sp.GetRequiredService<ILogger<HanimeProvider>>();
            return new HanimeProvider(playwrightClient, logger);
        });

        services.AddScoped<DlsiteProvider>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpNetworkClient>();
            var logger = sp.GetRequiredService<ILogger<DlsiteProvider>>();
            return new DlsiteProvider(httpClient, logger);
        });

        return services;
    }
}

/// <summary>
/// Service for managing Playwright lifecycle including browser creation and disposal.
/// Ensures proper resource cleanup on application shutdown.
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
        // Start browser initialization immediately
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
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] {
                    "--no-sandbox",
                    "--disable-blink-features=AutomationControlled",
                    "--disable-dev-shm-usage",
                    "--disable-gpu"
                }
            }).ConfigureAwait(false);
            
            _logger.LogInformation("Playwright browser initialized successfully");
            _browserTcs.SetResult(_browser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Playwright browser");
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

/// <summary>
/// Hosted service to ensure proper cleanup of Playwright resources on application shutdown.
/// </summary>
public class PlaywrightCleanupService : IHostedService
{
    private readonly PlaywrightService _playwrightService;
    private readonly PlaywrightContextManager _contextManager;
    private readonly ILogger<PlaywrightCleanupService> _logger;

    public PlaywrightCleanupService(
        PlaywrightService playwrightService,
        PlaywrightContextManager contextManager,
        ILogger<PlaywrightCleanupService> logger)
    {
        _playwrightService = playwrightService;
        _contextManager = contextManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Playwright cleanup service started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Stopping Playwright cleanup service...");
        
        try
        {
            // First dispose context manager (closes all contexts)
            await _contextManager.DisposeAsync().ConfigureAwait(false);
            
            // Then dispose the browser service
            await _playwrightService.DisposeAsync().ConfigureAwait(false);
            
            _logger.LogInformation("Playwright resources cleaned up successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Playwright cleanup");
        }
    }
}
