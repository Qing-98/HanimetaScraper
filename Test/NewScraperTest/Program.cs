using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScraperBackendService.Core.Abstractions;
using ScraperBackendService.Core.Net;
using ScraperBackendService.Core.Routing;
using ScraperBackendService.Core.Util;
using ScraperBackendService.Models;
using ScraperBackendService.Providers.DLsite;
using ScraperBackendService.Providers.Hanime;

namespace Test.NewScraperTest;

/// <summary>
/// Test application for DLsite and Hanime scraper providers using new architecture.
/// Demonstrates both HTTP and Playwright-based scraping approaches with interactive menu.
/// </summary>
class Program
{
    /// <summary>
    /// Main entry point for the test application.
    /// Provides interactive menu for different test scenarios.
    /// </summary>
    /// <param name="args">Command line arguments for direct test execution</param>
    /// <returns>Task representing the asynchronous operation</returns>
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== New Architecture DLsite & Hanime Scraper Test ===");
        Console.WriteLine();

        // Setup logging with console output and Information level
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Check if specific test is requested via command line
        if (args.Length > 0)
        {
            switch (args[0])
            {
                case "sourceurl-test":
                    await DlsiteSourceUrlTest.RunTestAsync();
                    return;
                case "tag-test":
                    await DlsiteTagTest.RunTestAsync();
                    return;
            }
        }
        // Display interactive menu for test selection
        DisplayTestMenu();
        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                await RunFullTestAsync(loggerFactory);
                break;
            case "2":
                await RunDLsiteTestAsync(loggerFactory);
                break;
            case "3":
                await RunHanimeTestAsync(loggerFactory);
                break;
            case "4":
                await RunIntegrationTestAsync();
                break;
            case "5":
                await RunConcurrentTestAsync();
                break;
            case "6":
                await DlsiteSourceUrlTest.RunTestAsync();
                break;
            case "7":
                await DlsiteTagTest.RunTestAsync();
                break;
            default:
                Console.WriteLine("Invalid choice, running full test...");
                await RunFullTestAsync(loggerFactory);
                break;
        }

        Console.WriteLine();
        Console.WriteLine("🎉 Test completed! Press any key to exit...");
        Console.ReadKey();
    }

    /// <summary>
    /// Displays the interactive test menu with available options.
    /// </summary>
    private static void DisplayTestMenu()
    {
        Console.WriteLine("Please select test mode:");
        Console.WriteLine("1. Full test (DLsite + Hanime)");
        Console.WriteLine("2. DLsite only test (HTTP)");
        Console.WriteLine("3. Hanime only test (Playwright)");
        Console.WriteLine("4. Backend API integration test");
        Console.WriteLine("5. Concurrent load test");
        Console.WriteLine("6. DLsite SourceUrl fix test");
        Console.WriteLine("7. DLsite Tag extraction test");
        Console.Write("Enter choice (1-7): ");
    }

    /// <summary>
    /// Runs comprehensive test covering both DLsite and Hanime providers.
    /// Uses HTTP client for DLsite and Playwright client for Hanime.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating loggers</param>
    /// <returns>Task representing the asynchronous operation</returns>
    private static async Task RunFullTestAsync(ILoggerFactory loggerFactory)
    {
        Console.WriteLine("🔍 Running Full Test Suite (DLsite + Hanime)");
        Console.WriteLine("=" + new string('=', 50));

        // Initialize Playwright browser for Hanime testing
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            PlaywrightResourceUtils.CreateLaunchOptions(headless: true));

        // Create network clients for different providers
        var playwrightClient = new PlaywrightNetworkClient(browser);
        var httpClient = new HttpNetworkClient(loggerFactory.CreateLogger<HttpNetworkClient>());

        // Define test cases with provider, input, route, and client combinations
        var testCases = new List<(string Provider, string Input, ScrapeRoute Route, INetworkClient Client)>
        {
            ("DLsite", "恋爱", ScrapeRoute.ByFilename, httpClient),
            ("DLsite", "RJ123456", ScrapeRoute.ById, httpClient),
            ("Hanime", "Love", ScrapeRoute.ByFilename, playwrightClient),
            ("Hanime", "86994", ScrapeRoute.ById, playwrightClient)
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        foreach (var (provider, input, route, client) in testCases)
        {
            Console.WriteLine($"🔍 Testing {provider} | Input: '{input}' | Mode: {route}");
            TestDisplayUtils.DisplaySeparator('-', 60);

            await ExecuteTestCaseAsync(provider, input, route, client, loggerFactory, cts.Token);

            Console.WriteLine();
            TestDisplayUtils.DisplaySeparator('=', 80);
            Console.WriteLine();

            // Brief delay to avoid overwhelming the target servers
            await Task.Delay(1000, cts.Token);
        }
    }

    /// <summary>
    /// Executes a single test case and displays results.
    /// </summary>
    private static async Task ExecuteTestCaseAsync(
        string provider,
        string input,
        ScrapeRoute route,
        INetworkClient client,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var startTime = DateTime.Now;
        try
        {
            var results = await TestProviderAsync(provider, input, route, client, loggerFactory, ct);
            var elapsed = DateTime.Now - startTime;

            TestDisplayUtils.DisplayTestSummary(provider, input, results.Count, elapsed);

            if (results.Count == 0)
            {
                Console.WriteLine("⚠️  No results found!");
            }
            else
            {
                Console.WriteLine($"✅ Successfully scraped {results.Count} results:");

                foreach (var (meta, index) in results.Select((m, i) => (m, i + 1)))
                {
                    TestDisplayUtils.DisplayDetailedMetadata(meta, index);
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            TestDisplayUtils.DisplayError($"{provider} scraping", ex);
        }
    }

    /// <summary>
    /// Runs DLsite-specific test using HTTP client only.
    /// Tests multiple search terms including Japanese text and product IDs.
    /// </summary>
    private static async Task RunDLsiteTestAsync(ILoggerFactory loggerFactory)
    {
        Console.WriteLine("🔍 Testing DLsite (HTTP client only)");
        Console.WriteLine("=" + new string('=', 40));

        var httpClient = new HttpNetworkClient(loggerFactory.CreateLogger<HttpNetworkClient>());
        var provider = new DlsiteProvider(httpClient, loggerFactory.CreateLogger<DlsiteProvider>());
        var orchestrator = new ScrapeOrchestrator(provider, httpClient, loggerFactory.CreateLogger<ScrapeOrchestrator>());

        var testInputs = new[] { "恋爱", "RJ01402281", "RJ01464954" };

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        foreach (var input in testInputs)
        {
            await ExecuteSingleProviderTestAsync("DLsite", input, orchestrator, cts.Token);
        }
    }

    /// <summary>
    /// Runs Hanime-specific test using Playwright client.
    /// Tests both text search and ID-based lookup with explicit route specification.
    /// </summary>
    private static async Task RunHanimeTestAsync(ILoggerFactory loggerFactory)
    {
        Console.WriteLine("🔍 Testing Hanime (Playwright client)");
        Console.WriteLine("=" + new string('=', 40));

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            PlaywrightResourceUtils.CreateLaunchOptions(headless: true));

        var playwrightClient = new PlaywrightNetworkClient(browser);
        var provider = new HanimeProvider(playwrightClient, loggerFactory.CreateLogger<HanimeProvider>());
        var orchestrator = new ScrapeOrchestrator(provider, playwrightClient, loggerFactory.CreateLogger<ScrapeOrchestrator>());

        // Define test cases with explicit routes
        var testCases = new[]
        {
            ("Love", ScrapeRoute.Auto, "Text search with Auto mode"),
            ("86994", ScrapeRoute.ById, "ID lookup with explicit ById mode")
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        foreach (var (input, route, description) in testCases)
        {
            Console.WriteLine($"📝 Test: {description}");
            Console.WriteLine($"📝 Searching: '{input}' (Route: {route})");
            var startTime = DateTime.Now;

            try
            {
                var results = await orchestrator.FetchAsync(input, route, 2, cts.Token);
                var elapsed = DateTime.Now - startTime;

                TestDisplayUtils.DisplayTestSummary("Hanime", input, results.Count, elapsed);

                if (results.Count == 0)
                {
                    Console.WriteLine("⚠️  No results found");
                }
                else
                {
                    foreach (var (meta, index) in results.Select((m, i) => (m, i + 1)))
                    {
                        TestDisplayUtils.DisplayDetailedMetadata(meta, index);
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                TestDisplayUtils.DisplayError($"Hanime search", ex);
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Executes test for a single provider with given input.
    /// </summary>
    private static async Task ExecuteSingleProviderTestAsync(
        string providerName,
        string input,
        ScrapeOrchestrator orchestrator,
        CancellationToken ct)
    {
        Console.WriteLine($"📝 Searching: '{input}'");
        var startTime = DateTime.Now;

        try
        {
            var results = await orchestrator.FetchAsync(input, ScrapeRoute.Auto, 2, ct);
            var elapsed = DateTime.Now - startTime;

            TestDisplayUtils.DisplayTestSummary(providerName, input, results.Count, elapsed);

            if (results.Count == 0)
            {
                Console.WriteLine("⚠️  No results found");
            }
            else
            {
                foreach (var (meta, index) in results.Select((m, i) => (m, i + 1)))
                {
                    TestDisplayUtils.DisplayDetailedMetadata(meta, index);
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            TestDisplayUtils.DisplayError($"{providerName} search", ex);
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Runs integration test by calling backend API endpoints.
    /// Simulates how Jellyfin frontend would interact with the backend service.
    /// </summary>
    private static async Task RunIntegrationTestAsync()
    {
        Console.WriteLine("🔍 Running Backend API Integration Test");
        Console.WriteLine("=" + new string('=', 40));
        
        Console.Write("Please enter backend service URL (e.g., http://localhost:8585): ");
        var backendUrl = Console.ReadLine() ?? "http://localhost:8585";
        Console.Write("Please enter Token (optional, press Enter to skip): ");
        var token = Console.ReadLine();

        await BackendApiIntegrationTest.TestHanimeApiAsync(backendUrl, token);
        await BackendApiIntegrationTest.TestDlsiteApiAsync(backendUrl, token);
    }

    /// <summary>
    /// Runs concurrent test to simulate multiple simultaneous API requests.
    /// Tests system behavior under load from multiple Jellyfin plugin instances.
    /// </summary>
    private static async Task RunConcurrentTestAsync()
    {
        Console.WriteLine("🔍 Running Concurrent Load Test");
        Console.WriteLine("=" + new string('=', 40));
        
        Console.Write("Please enter backend service URL (e.g., http://localhost:8585): ");
        var backendUrl = Console.ReadLine() ?? "http://localhost:8585";
        Console.Write("Please enter Token (optional, press Enter to skip): ");
        var token = Console.ReadLine();
        Console.Write("Please enter concurrent request count (default: 5): ");
        var countStr = Console.ReadLine();

        int concurrentCount = 5;
        int.TryParse(countStr, out concurrentCount);

        await BackendApiIntegrationTest.TestConcurrentApiAsync(backendUrl, token, concurrentCount);
    }

    /// <summary>
    /// Creates and tests a specific provider with given parameters.
    /// Factory method for creating provider instances based on provider name.
    /// </summary>
    private static async Task<List<Metadata>> TestProviderAsync(
        string providerName,
        string input,
        ScrapeRoute route,
        INetworkClient client,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        IMediaProvider provider = providerName switch
        {
            "DLsite" => new DlsiteProvider(client, loggerFactory.CreateLogger<DlsiteProvider>()),
            "Hanime" => new HanimeProvider(client, loggerFactory.CreateLogger<HanimeProvider>()),
            _ => throw new ArgumentException($"Unknown provider: {providerName}")
        };

        var orchestrator = new ScrapeOrchestrator(
            provider,
            client,
            loggerFactory.CreateLogger<ScrapeOrchestrator>());

        return await orchestrator.FetchAsync(input, route, maxResults: 3, ct);
    }
}
