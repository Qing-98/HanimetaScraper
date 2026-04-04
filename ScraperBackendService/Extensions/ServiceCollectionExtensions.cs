using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using ScraperBackendService.Browser;
using ScraperBackendService.Configuration;
using ScraperBackendService.Core.Net;
using System.Net;
using System.Text.RegularExpressions;

namespace ScraperBackendService.Extensions;

/// <summary>
/// Service registration extension methods for dependency injection configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly Regex ChromeUserAgentRegex = new("Chrome/[0-9]+(?:\\.[0-9]+){0,3}", RegexOptions.Compiled);

    public static IServiceCollection AddScrapingServices(this IServiceCollection services, ServiceConfiguration config)
    {
        services.AddSingleton<IOptions<ServiceConfiguration>>(Options.Create(config));

        services.AddSingleton<PlaywrightService>();

        services.AddSingleton<IBrowser>(sp =>
        {
            var playwrightService = sp.GetRequiredService<PlaywrightService>();
            return playwrightService
                .GetBrowserAsync()
                .WaitAsync(TimeSpan.FromSeconds(60))
                .GetAwaiter()
                .GetResult();
        });

        services.AddSingleton<PlaywrightContextManager>(sp =>
        {
            var browser = sp.GetRequiredService<IBrowser>();
            var logger = sp.GetRequiredService<ILogger<PlaywrightContextManager>>();
            var options = new PlaywrightClientOptions
            {
                ChallengeAutoWaitMs = config.ChallengeAutoWaitSeconds * 1000,
                ChallengeAutoWaitSlowMs = config.ChallengeAutoWaitSlowSeconds * 1000,
                EnableManualChallengeResolution = config.EnableManualChallengeResolution,
                ManualChallengeTimeoutSeconds = config.ManualChallengeTimeoutSeconds,
                ManualChallengeWindowWidth = config.ManualChallengeWindowWidth,
                ManualChallengeWindowHeight = config.ManualChallengeWindowHeight,
                EnableCookiePersistence = config.EnableCookiePersistence,
                AutoLoadCookiesOnContextCreation = config.AutoLoadCookiesOnContextCreation,
                CookieStorageDirectory = config.CookieStorageDirectory
            };

            AlignUserAgentWithBrowserVersion(options, browser.Version, logger);
            return new PlaywrightContextManager(browser, logger, options);
        });

        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ChallengeDetector>>();
            return new ChallengeDetector(logger);
        });

        services.AddScoped<PlaywrightNetworkClient>(sp =>
        {
            var ctxMgr = sp.GetRequiredService<PlaywrightContextManager>();
            var logger = sp.GetRequiredService<ILogger<PlaywrightNetworkClient>>();
            var detector = sp.GetRequiredService<ChallengeDetector>();
            return new PlaywrightNetworkClient(ctxMgr, logger, detector);
        });

        services.AddHttpClient<HttpNetworkClient>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118 Safari/537.36");
            http.DefaultRequestHeaders.Accept.ParseAdd(
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            http.DefaultRequestHeaders.AcceptLanguage.ParseAdd(
                "en-US,en;q=0.9,ja;q=0.8,zh-CN;q=0.7");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(90),
            MaxConnectionsPerServer = 12,
            EnableMultipleHttp2Connections = true,
            UseCookies = false
        });

        foreach (var providerConfig in Providers._Registry.ProviderRegistry.GetAllProviders())
        {
            Providers._Registry.ProviderRegistry.RegisterProvider(services, providerConfig, config);
        }

        return services;
    }

    private static void AlignUserAgentWithBrowserVersion(PlaywrightClientOptions options, string? browserVersion, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(browserVersion))
        {
            return;
        }

        if (!ChromeUserAgentRegex.IsMatch(options.UserAgent))
        {
            return;
        }

        var updatedUserAgent = ChromeUserAgentRegex.Replace(options.UserAgent, $"Chrome/{browserVersion}", 1);
        if (string.Equals(updatedUserAgent, options.UserAgent, StringComparison.Ordinal))
        {
            return;
        }

        options.UserAgent = updatedUserAgent;
        logger.LogDebug("PlaywrightOptions", $"Aligned UserAgent with browser version: {browserVersion}");
    }
}