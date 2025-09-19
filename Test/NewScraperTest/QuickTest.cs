using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ScraperBackendService.Core.Net;
using ScraperBackendService.Core.Pipeline;
using ScraperBackendService.Core.Routing;
using ScraperBackendService.Providers.DLsite;
using ScraperBackendService.Providers.Hanime;

namespace Test.NewScraperTest;

/// <summary>
/// Simplified quick test class for individual provider testing.
/// Provides lightweight testing methods for rapid development and debugging.
/// </summary>
/// <example>
/// Usage examples:
/// - await QuickTest.TestDLsiteOnly(); // Test DLsite provider with HTTP client
/// - await QuickTest.TestHanimeOnly(); // Test Hanime provider with Playwright client
/// </example>
public static class QuickTest
{
    /// <summary>
    /// Performs a quick test of the DLsite provider using HTTP client.
    /// Tests search functionality with Japanese keywords and displays formatted results.
    /// </summary>
    /// <returns>Task representing the asynchronous operation</returns>
    /// <example>
    /// // Run DLsite-only test
    /// await QuickTest.TestDLsiteOnly();
    /// 
    /// Expected output:
    /// - Search results for "恋爱" (love in Japanese)
    /// - Product IDs, titles, descriptions, and genre tags
    /// - Limited to 2 results for quick testing
    /// </example>
    public static async Task TestDLsiteOnly()
    {
        Console.WriteLine("=== DLsite Quick Test ===");

        using var loggerFactory = CreateLoggerFactory();

        var httpClient = new HttpNetworkClient(loggerFactory.CreateLogger<HttpNetworkClient>());
        var provider = new DlsiteProvider(httpClient, loggerFactory.CreateLogger<DlsiteProvider>());
        var orchestrator = new ScrapeOrchestrator(provider, httpClient, loggerFactory.CreateLogger<ScrapeOrchestrator>());

        // Test search with Japanese keyword
        var results = await orchestrator.FetchAsync("恋爱", ScrapeRoute.ByFilename, 2, default);
        
        DisplayResults(results, "DLsite");
    }

    /// <summary>
    /// Performs a quick test of the Hanime provider using Playwright client.
    /// Tests search functionality with English keywords and displays formatted results.
    /// </summary>
    /// <returns>Task representing the asynchronous operation</returns>
    /// <example>
    /// // Run Hanime-only test
    /// await QuickTest.TestHanimeOnly();
    /// 
    /// Expected output:
    /// - Search results for "Love" keyword
    /// - Video IDs, titles, descriptions, and genre tags
    /// - Limited to 2 results for quick testing
    /// </example>
    public static async Task TestHanimeOnly()
    {
        Console.WriteLine("=== Hanime Quick Test ===");

        using var loggerFactory = CreateLoggerFactory();

        // Initialize Playwright for browser automation
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });

        var playwrightClient = new PlaywrightNetworkClient(browser);
        var provider = new HanimeProvider(playwrightClient, loggerFactory.CreateLogger<HanimeProvider>());
        var orchestrator = new ScrapeOrchestrator(provider, playwrightClient, loggerFactory.CreateLogger<ScrapeOrchestrator>());

        // Test search with English keyword
        var results = await orchestrator.FetchAsync("Love", ScrapeRoute.ByFilename, 2, default);
        
        DisplayResults(results, "Hanime");
    }

    /// <summary>
    /// Creates a logger factory with console output and Information level logging.
    /// Standardizes logging configuration across quick test methods.
    /// </summary>
    /// <returns>Configured ILoggerFactory instance</returns>
    /// <example>
    /// var loggerFactory = CreateLoggerFactory();
    /// var logger = loggerFactory.CreateLogger<MyClass>();
    /// </example>
    private static ILoggerFactory CreateLoggerFactory()
    {
        return LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
    }

    /// <summary>
    /// Displays search results in a formatted, user-friendly manner.
    /// Shows result count, individual item details including ID, title, description, and genres.
    /// </summary>
    /// <param name="results">List of metadata results to display</param>
    /// <param name="providerName">Name of the provider for display purposes</param>
    /// <example>
    /// var results = await orchestrator.FetchAsync("search term", ScrapeRoute.Auto, 5, ct);
    /// DisplayResults(results, "DLsite");
    /// 
    /// Output format:
    /// Found 2 results
    /// - RJ123456: Title Name
    ///   Description: Brief description...
    ///   Tags: tag1, tag2, tag3
    /// </example>
    private static void DisplayResults(System.Collections.Generic.List<ScraperBackendService.Models.HanimeMetadata> results, string providerName)
    {
        Console.WriteLine($"Found {results.Count} results from {providerName}");
        
        if (results.Count == 0)
        {
            Console.WriteLine("⚠️  No results found");
            return;
        }

        foreach (var result in results)
        {
            Console.WriteLine($"- {result.ID}: {result.Title}");
            
            // Display truncated description if available
            if (!string.IsNullOrWhiteSpace(result.Description))
            {
                var desc = result.Description.Length > 50 
                    ? result.Description[..50] + "..." 
                    : result.Description;
                Console.WriteLine($"  Description: {desc}");
            }
            
            // Display genres/tags
            if (result.Genres.Count > 0)
            {
                Console.WriteLine($"  Tags: {string.Join(", ", result.Genres)}");
            }
            
            Console.WriteLine();
        }
    }
}