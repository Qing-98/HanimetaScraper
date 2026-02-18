using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Test.NewScraperTest;

/// <summary>
/// HTTP API-based test program that simulates Jellyfin frontend behavior.
/// All tests communicate with backend via HTTP requests instead of direct code calls.
/// This is the correct way to test as it mimics how the Jellyfin plugin actually works.
/// </summary>
class Program
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private static string _backendUrl = "http://localhost:8585";
    private static string? _apiToken = null;

    /// <summary>
    /// Main entry point for the test application.
    /// Provides interactive menu for different HTTP API test scenarios.
    /// </summary>
    /// <param name="args">Command line arguments for direct test execution</param>
    /// <returns>Task representing the asynchronous operation</returns>
    static async Task Main(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  Scraper Backend HTTP API Test (Frontend Simulator)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("⚠️  Important: Make sure ScraperBackendService is running!");
        Console.WriteLine("   Start it with: cd ScraperBackendService && dotnet run");
        Console.WriteLine();

        // Get backend URL from user
        Console.Write("Enter backend URL (default: http://localhost:8585): ");
        var backendInput = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(backendInput))
        {
            _backendUrl = backendInput.TrimEnd('/');
        }

        Console.Write("Enter API token (optional, press Enter to skip): ");
        _apiToken = Console.ReadLine();
        
        if (!string.IsNullOrWhiteSpace(_apiToken))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Token", _apiToken);
            Console.WriteLine("✅ API token configured");
        }

        Console.WriteLine();

        // Test backend connectivity first
        if (!await TestBackendConnectivity())
        {
            Console.WriteLine();
            Console.WriteLine("❌ Cannot connect to backend service!");
            Console.WriteLine("   Please start ScraperBackendService first.");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        // Check if specific test is requested via command line
        if (args.Length > 0)
        {
            switch (args[0].ToLower())
            {
                case "full":
                    await RunFullTestAsync();
                    return;
                case "dlsite":
                    await RunDLsiteTestAsync();
                    return;
                case "hanime":
                    await RunHanimeTestAsync();
                    return;
                case "health":
                    await RunHealthCheckAsync();
                    return;
                case "concurrent":
                    await RunConcurrentTestAsync();
                    return;
                default:
                    Console.WriteLine($"Unknown test: {args[0]}");
                    break;
            }
        }

        // Display interactive menu for test selection
        while (true)
        {
            DisplayTestMenu();
            var choice = Console.ReadLine();

            if (choice == "0" || choice?.ToLower() == "q")
            {
                break;
            }

            switch (choice)
            {
                case "1":
                    await RunFullTestAsync();
                    break;
                case "2":
                    await RunDLsiteTestAsync();
                    break;
                case "3":
                    await RunHanimeTestAsync();
                    break;
                case "4":
                    await RunHealthCheckAsync();
                    break;
                case "5":
                    await RunConcurrentTestAsync();
                    break;
                case "6":
                    await RunCacheStatsAsync();
                    break;
                case "7":
                    await RunCustomTestAsync();
                    break;
                default:
                    Console.WriteLine("❌ Invalid choice");
                    break;
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            Console.Clear();
        }

        Console.WriteLine();
        Console.WriteLine("👋 Goodbye!");
    }

    /// <summary>
    /// Displays the interactive test menu with available options.
    /// </summary>
    private static void DisplayTestMenu()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("                     Test Menu");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("  1. Full test (DLsite + Hanime via HTTP API)");
        Console.WriteLine("  2. DLsite API test only");
        Console.WriteLine("  3. Hanime API test only");
        Console.WriteLine("  4. Health check and service info");
        Console.WriteLine("  5. Concurrent load test");
        Console.WriteLine("  6. Cache statistics");
        Console.WriteLine("  7. Custom test (enter your own parameters)");
        Console.WriteLine("  0. Exit");
        Console.WriteLine();
        Console.Write("Enter choice (0-7): ");
    }

    /// <summary>
    /// Tests backend connectivity before running tests.
    /// </summary>
    private static async Task<bool> TestBackendConnectivity()
    {
        Console.WriteLine("🔍 Testing backend connectivity...");
        try
        {
            var response = await _httpClient.GetAsync($"{_backendUrl}/health");
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Backend is reachable at {_backendUrl}");
                return true;
            }
            else
            {
                Console.WriteLine($"⚠️  Backend returned status {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Connection failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Runs comprehensive test covering both DLsite and Hanime providers via HTTP API.
    /// </summary>
    private static async Task RunFullTestAsync()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("                     Full API Test");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        
        // DLsite tests
        Console.WriteLine("🔵 DLsite API Tests:");
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        await TestApiEndpoint("/api/dlsite/search?title=恋爱&max=2", "DLsite Search (恋爱)");
        await Task.Delay(1000);
        
        await TestApiEndpoint("/api/dlsite/RJ01402281", "DLsite Detail (RJ01402281)");
        await Task.Delay(1000);
        
        await TestApiEndpoint("/api/dlsite/search?title=ボイス&max=2", "DLsite Search (ボイス)");
        await Task.Delay(1000);
        
        Console.WriteLine();
        
        // Hanime tests
        Console.WriteLine("🟢 Hanime API Tests:");
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        await TestApiEndpoint("/api/hanime/search?title=Love&max=2", "Hanime Search (Love)");
        await Task.Delay(1000);
        
        await TestApiEndpoint("/api/hanime/86994", "Hanime Detail (86994)");
        await Task.Delay(1000);
        
        await TestApiEndpoint("/api/hanime/search?title=school&max=2", "Hanime Search (school)");
        
        Console.WriteLine();
        Console.WriteLine("✅ Full test completed!");
    }

    /// <summary>
    /// Runs DLsite-specific API tests.
    /// </summary>
    private static async Task RunDLsiteTestAsync()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("                   DLsite API Test");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        
        await TestApiEndpoint("/api/dlsite/search?title=恋爱&max=3", "DLsite Search (恋爱)");
        await Task.Delay(1000);
        
        await TestApiEndpoint("/api/dlsite/RJ01402281", "DLsite Detail (RJ01402281)");
        await Task.Delay(1000);
        
        await TestApiEndpoint("/api/dlsite/RJ01464954", "DLsite Detail (RJ01464954)");
        await Task.Delay(1000);
        
        await TestApiEndpoint("/api/dlsite/search?title=ボイス&max=2", "DLsite Voice Search");
        
        Console.WriteLine();
        Console.WriteLine("✅ DLsite tests completed!");
    }

    /// <summary>
    /// Runs Hanime-specific API tests.
    /// </summary>
    private static async Task RunHanimeTestAsync()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("                   Hanime API Test");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        
        await TestApiEndpoint("/api/hanime/search?title=Love&max=3", "Hanime Search (Love)");
        await Task.Delay(1000);
        
        await TestApiEndpoint("/api/hanime/86994", "Hanime Detail (86994)");
        await Task.Delay(1000);
        
        await TestApiEndpoint("/api/hanime/search?title=school&max=2", "Hanime School Search");
        await Task.Delay(1000);
        
        await TestApiEndpoint("/api/hanime/search?title=fantasy&max=2", "Hanime Fantasy Search");
        
        Console.WriteLine();
        Console.WriteLine("✅ Hanime tests completed!");
    }

    /// <summary>
    /// Runs health check and service info tests.
    /// </summary>
    private static async Task RunHealthCheckAsync()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("               Service Health Check");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        
        await TestApiEndpoint("/", "Service Info");
        await Task.Delay(500);
        
        await TestApiEndpoint("/health", "Health Status");
        
        Console.WriteLine();
        Console.WriteLine("✅ Health check completed!");
    }

    /// <summary>
    /// Runs concurrent load test to simulate multiple requests.
    /// </summary>
    private static async Task RunConcurrentTestAsync()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("               Concurrent Load Test");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        
        Console.Write("Enter concurrent request count (default: 5): ");
        var countStr = Console.ReadLine();
        int concurrentCount = int.TryParse(countStr, out var c) ? c : 5;

        Console.WriteLine();
        Console.WriteLine($"🚀 Launching {concurrentCount * 2} concurrent requests ({concurrentCount} per provider)...");
        Console.WriteLine();

        var tasks = new List<Task>();
        var startTime = DateTime.Now;

        for (int i = 0; i < concurrentCount; i++)
        {
            int index = i + 1;
            tasks.Add(TestApiEndpoint("/api/hanime/search?title=test&max=1", $"Hanime #{index}", silent: true));
            tasks.Add(TestApiEndpoint("/api/dlsite/search?title=test&max=1", $"DLsite #{index}", silent: true));
        }

        await Task.WhenAll(tasks);
        
        var elapsed = DateTime.Now - startTime;
        
        Console.WriteLine();
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        Console.WriteLine($"✅ Completed {tasks.Count} requests in {elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"   Average: {elapsed.TotalMilliseconds / tasks.Count:F0}ms per request");
        Console.WriteLine($"   Throughput: {tasks.Count / elapsed.TotalSeconds:F1} req/s");
        Console.WriteLine("─────────────────────────────────────────────────────────────");
    }

    /// <summary>
    /// Displays cache statistics from the backend.
    /// </summary>
    private static async Task RunCacheStatsAsync()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("                  Cache Statistics");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        
        await TestApiEndpoint("/cache/stats", "Cache Stats");
        
        Console.WriteLine();
        Console.WriteLine("✅ Cache stats retrieved!");
    }

    /// <summary>
    /// Allows user to run custom API tests with their own parameters.
    /// </summary>
    private static async Task RunCustomTestAsync()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("                    Custom Test");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        
        Console.WriteLine("Select provider:");
        Console.WriteLine("1. DLsite");
        Console.WriteLine("2. Hanime");
        Console.Write("Choice: ");
        var providerChoice = Console.ReadLine();

        Console.WriteLine();
        Console.WriteLine("Select operation:");
        Console.WriteLine("1. Search");
        Console.WriteLine("2. Get by ID");
        Console.Write("Choice: ");
        var operationChoice = Console.ReadLine();

        Console.WriteLine();
        Console.Write("Enter search term or ID: ");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("❌ Invalid input");
            return;
        }

        Console.Write("Max results (default: 5): ");
        var maxStr = Console.ReadLine();
        int maxResults = int.TryParse(maxStr, out var m) ? m : 5;

        Console.WriteLine();

        string endpoint = "";
        string description = "";

        if (providerChoice == "1") // DLsite
        {
            if (operationChoice == "1") // Search
            {
                endpoint = $"/api/dlsite/search?title={Uri.EscapeDataString(input)}&max={maxResults}";
                description = $"DLsite Search ({input})";
            }
            else // Get by ID
            {
                endpoint = $"/api/dlsite/{input}";
                description = $"DLsite Detail ({input})";
            }
        }
        else // Hanime
        {
            if (operationChoice == "1") // Search
            {
                endpoint = $"/api/hanime/search?title={Uri.EscapeDataString(input)}&max={maxResults}";
                description = $"Hanime Search ({input})";
            }
            else // Get by ID
            {
                endpoint = $"/api/hanime/{input}";
                description = $"Hanime Detail ({input})";
            }
        }

        await TestApiEndpoint(endpoint, description);
        
        Console.WriteLine();
        Console.WriteLine("✅ Custom test completed!");
    }

    /// <summary>
    /// Tests a specific API endpoint and displays results.
    /// </summary>
    private static async Task TestApiEndpoint(
        string endpoint, 
        string testName,
        bool silent = false)
    {
        var url = $"{_backendUrl}{endpoint}";
        
        if (!silent)
        {
            Console.WriteLine($"📡 {testName}");
            Console.WriteLine($"   URL: {url}");
        }

        try
        {
            var startTime = DateTime.Now;
            var response = await _httpClient.GetAsync(url);
            var elapsed = DateTime.Now - startTime;
            
            var content = await response.Content.ReadAsStringAsync();
            
            if (!silent)
            {
                Console.WriteLine($"   Status: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"   Time: {elapsed.TotalMilliseconds:F0}ms");
                Console.WriteLine($"   Size: {content.Length} bytes");

                if (response.IsSuccessStatusCode)
                {
                    // Try to parse and pretty print JSON
                    try
                    {
                        var json = JsonSerializer.Deserialize<JsonElement>(content);
                        var prettyJson = JsonSerializer.Serialize(json, new JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        });
                        
                        // Show truncated response for readability
                        if (prettyJson.Length > 1000)
                        {
                            Console.WriteLine($"   Response (truncated):");
                            Console.WriteLine($"{prettyJson[..1000]}...");
                        }
                        else
                        {
                            Console.WriteLine($"   Response:");
                            Console.WriteLine($"{prettyJson}");
                        }
                    }
                    catch
                    {
                        // If not JSON, show raw
                        var display = content.Length > 500 ? content[..500] + "..." : content;
                        Console.WriteLine($"   Response: {display}");
                    }
                    
                    Console.WriteLine("   ✅ Success");
                }
                else
                {
                    Console.WriteLine($"   ❌ Failed: {content}");
                }
                
                Console.WriteLine();
            }
            else
            {
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ {testName} - {elapsed.TotalMilliseconds:F0}ms");
                }
                else
                {
                    Console.WriteLine($"❌ {testName} - {response.StatusCode}");
                }
            }
        }
        catch (HttpRequestException ex)
        {
            if (!silent)
            {
                Console.WriteLine($"   ❌ HTTP Error: {ex.Message}");
                Console.WriteLine($"   💡 Make sure backend is running at {_backendUrl}");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"❌ {testName} - Connection failed");
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine(!silent 
                ? "   ❌ Request timeout" 
                : $"❌ {testName} - Timeout");
        }
        catch (Exception ex)
        {
            Console.WriteLine(!silent 
                ? $"   ❌ Error: {ex.Message}" 
                : $"❌ {testName} - Error: {ex.Message}");
        }
    }
}
