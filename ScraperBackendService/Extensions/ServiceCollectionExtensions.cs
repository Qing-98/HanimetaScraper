using Microsoft.Playwright;
using ScraperBackendService.Configuration;
using ScraperBackendService.Core.Net;
using ScraperBackendService.Providers.DLsite;
using ScraperBackendService.Providers.Hanime;

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
            opt.EnableDetailedLogging = config.EnableDetailedLogging;
            opt.MaxConcurrentRequests = config.MaxConcurrentRequests;
            opt.RequestTimeoutSeconds = config.RequestTimeoutSeconds;
        });

        // Register Playwright browser as singleton for resource efficiency
        services.AddSingleton(async sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Initializing Playwright browser...");

            var pw = await Playwright.CreateAsync();
            var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] {
                    "--no-sandbox",                           // Disable sandbox for containerized environments
                    "--disable-blink-features=AutomationControlled", // Avoid detection as automated browser
                    "--disable-dev-shm-usage",               // Reduce memory usage
                    "--disable-gpu"                          // Disable GPU acceleration for stability
                }
            });

            logger.LogInformation("Playwright browser initialized successfully");
            return browser;
        });

        // Register network clients with appropriate lifetimes
        services.AddScoped<PlaywrightNetworkClient>(sp =>
        {
            var browser = sp.GetRequiredService<Task<IBrowser>>().GetAwaiter().GetResult();
            return new PlaywrightNetworkClient(browser);
        });

        services.AddScoped<HttpNetworkClient>();

        // Register content providers with their preferred network clients
        services.AddScoped<HanimeProvider>(sp =>
        {
            // Hanime uses Playwright for JavaScript-heavy content
            var playwrightClient = sp.GetRequiredService<PlaywrightNetworkClient>();
            var logger = sp.GetRequiredService<ILogger<HanimeProvider>>();
            return new HanimeProvider(playwrightClient, logger);
        });

        services.AddScoped<DlsiteProvider>(sp =>
        {
            // DLsite uses HTTP client for efficient static content scraping
            var httpClient = sp.GetRequiredService<HttpNetworkClient>();
            var logger = sp.GetRequiredService<ILogger<DlsiteProvider>>();
            return new DlsiteProvider(httpClient, logger);
        });

        return services;
    }
}
