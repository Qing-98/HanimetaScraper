using System.Net;
using System.Net.Http;
using System.Text.Json;
using Test.NewScraperTest.Suites;
using Test.NewScraperTest.Testing;

namespace Test.NewScraperTest;

internal static class Program
{
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = false
    })
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private static async Task Main(string[] args)
    {
        Console.WriteLine("Scraper Backend Test Runner");

        var backendUrl = ReadBackendUrl();
        var apiToken = ReadApiToken();
        ConfigureToken(apiToken);

        var authEnabled = await DetectAuthEnabledAsync(backendUrl).ConfigureAwait(false);
        var context = new TestContext(backendUrl, HttpClient, apiToken, authEnabled);

        if (authEnabled && string.IsNullOrWhiteSpace(apiToken))
        {
            Console.WriteLine("Warning: backend auth is enabled but token is empty. Protected endpoints may fail.");
        }

        if (args.Length > 0)
        {
            await RunByArgumentAsync(context, args[0]).ConfigureAwait(false);
            return;
        }

        await RunInteractiveAsync(context).ConfigureAwait(false);
    }

    private static async Task RunInteractiveAsync(TestContext context)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("1. Full coverage suite");
            Console.WriteLine("2. Core service suite");
            Console.WriteLine("3. DLsite suite");
            Console.WriteLine("4. Hanime suite");
            Console.WriteLine("5. Cache suite");
            Console.WriteLine("6. Redirect suite");
            Console.WriteLine("7. Concurrency suite");
            Console.WriteLine("8. Mechanism suite (slot/cache)");
            Console.WriteLine("0. Exit");
            Console.Write("Choice: ");

            var choice = Console.ReadLine();
            if (choice == "0")
            {
                return;
            }

            var allResults = choice switch
            {
                "1" => await RunFullCoverageAsync(context).ConfigureAwait(false),
                "2" => await TestSuiteRunner.RunSuiteAsync(context, CoreServiceSuite.Create()).ConfigureAwait(false),
                "3" => await TestSuiteRunner.RunSuiteAsync(context, ProviderSuite.CreateDlsiteSuite()).ConfigureAwait(false),
                "4" => await TestSuiteRunner.RunSuiteAsync(context, ProviderSuite.CreateHanimeSuite()).ConfigureAwait(false),
                "5" => await TestSuiteRunner.RunSuiteAsync(context, CacheSuite.Create()).ConfigureAwait(false),
                "6" => await TestSuiteRunner.RunSuiteAsync(context, RedirectSuite.Create()).ConfigureAwait(false),
                "7" => await RunConcurrencyInteractiveAsync(context).ConfigureAwait(false),
                "8" => await TestSuiteRunner.RunSuiteAsync(context, MechanismSuite.Create()).ConfigureAwait(false),
                _ => Array.Empty<ApiTestResult>()
            };

            if (allResults.Count > 0)
            {
                TestSuiteRunner.PrintOverallSummary(allResults);
            }
        }
    }

    private static async Task<IReadOnlyCollection<ApiTestResult>> RunConcurrencyInteractiveAsync(TestContext context)
    {
        Console.Write("Concurrent requests per provider (default 5): ");
        var input = Console.ReadLine();
        var count = int.TryParse(input, out var parsed) ? parsed : 5;

        var suite = ConcurrencySuite.Create(count);
        return await TestSuiteRunner.RunSuiteParallelAsync(context, suite).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyCollection<ApiTestResult>> RunFullCoverageAsync(TestContext context)
    {
        var results = new List<ApiTestResult>();

        results.AddRange(await TestSuiteRunner.RunSuiteAsync(context, CoreServiceSuite.Create()).ConfigureAwait(false));
        results.AddRange(await TestSuiteRunner.RunSuiteAsync(context, ProviderSuite.CreateDlsiteSuite()).ConfigureAwait(false));
        results.AddRange(await TestSuiteRunner.RunSuiteAsync(context, ProviderSuite.CreateHanimeSuite()).ConfigureAwait(false));
        results.AddRange(await TestSuiteRunner.RunSuiteAsync(context, CacheSuite.Create()).ConfigureAwait(false));
        results.AddRange(await TestSuiteRunner.RunSuiteAsync(context, RedirectSuite.Create()).ConfigureAwait(false));
        results.AddRange(await TestSuiteRunner.RunSuiteAsync(context, MechanismSuite.Create()).ConfigureAwait(false));
        results.AddRange(await TestSuiteRunner.RunSuiteParallelAsync(context, ConcurrencySuite.Create(5)).ConfigureAwait(false));

        return results;
    }

    private static async Task RunByArgumentAsync(TestContext context, string arg)
    {
        IReadOnlyCollection<ApiTestResult> results = arg.ToLowerInvariant() switch
        {
            "full" => await RunFullCoverageAsync(context).ConfigureAwait(false),
            "core" => await TestSuiteRunner.RunSuiteAsync(context, CoreServiceSuite.Create()).ConfigureAwait(false),
            "dlsite" => await TestSuiteRunner.RunSuiteAsync(context, ProviderSuite.CreateDlsiteSuite()).ConfigureAwait(false),
            "hanime" => await TestSuiteRunner.RunSuiteAsync(context, ProviderSuite.CreateHanimeSuite()).ConfigureAwait(false),
            "cache" => await TestSuiteRunner.RunSuiteAsync(context, CacheSuite.Create()).ConfigureAwait(false),
            "redirect" => await TestSuiteRunner.RunSuiteAsync(context, RedirectSuite.Create()).ConfigureAwait(false),
            "mechanism" => await TestSuiteRunner.RunSuiteAsync(context, MechanismSuite.Create()).ConfigureAwait(false),
            "concurrent" => await TestSuiteRunner.RunSuiteParallelAsync(context, ConcurrencySuite.Create(5)).ConfigureAwait(false),
            _ => Array.Empty<ApiTestResult>()
        };

        if (results.Count == 0)
        {
            Console.WriteLine($"Unknown suite argument: {arg}");
            return;
        }

        TestSuiteRunner.PrintOverallSummary(results);
    }

    private static string ReadBackendUrl()
    {
        Console.Write("Backend URL (default http://localhost:8585): ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? "http://localhost:8585" : input.TrimEnd('/');
    }

    private static string? ReadApiToken()
    {
        Console.Write("API token (optional): ");
        var token = Console.ReadLine();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static void ConfigureToken(string? apiToken)
    {
        HttpClient.DefaultRequestHeaders.Remove("X-API-Token");
        if (!string.IsNullOrWhiteSpace(apiToken))
        {
            HttpClient.DefaultRequestHeaders.Add("X-API-Token", apiToken);
        }
    }

    private static async Task<bool> DetectAuthEnabledAsync(string backendUrl)
    {
        try
        {
            using var response = await HttpClient.GetAsync($"{backendUrl}/").ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return root.TryGetProperty("data", out var data)
                   && data.ValueKind == JsonValueKind.Object
                   && data.TryGetProperty("authEnabled", out var authEnabled)
                   && authEnabled.ValueKind is JsonValueKind.True or JsonValueKind.False
                   && authEnabled.GetBoolean();
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
