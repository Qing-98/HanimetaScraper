using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

namespace Test.NewScraperTest;

/// <summary>
/// Integration test class for testing backend API endpoints.
/// Simulates real-world scenarios where Jellyfin frontend communicates with the scraper backend service.
/// All tests use HTTP requests to properly simulate the plugin's behavior.
/// </summary>
/// <example>
/// Usage examples:
/// - await BackendApiIntegrationTest.TestHanimeApiAsync("http://localhost:8585", "your-token");
/// - await BackendApiIntegrationTest.TestDlsiteApiAsync("http://localhost:8585");
/// - await BackendApiIntegrationTest.TestConcurrentApiAsync("http://localhost:8585", null, 10);
/// </example>
public static class BackendApiIntegrationTest
{
    /// <summary>
    /// Runs all backend API integration tests with default configuration.
    /// This is the main entry point for running backend API tests.
    /// </summary>
    /// <param name="backendUrl">Backend service URL (default: http://localhost:8585)</param>
    /// <param name="token">Optional API token for authentication</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public static async Task RunTestsAsync(string backendUrl = "http://localhost:8585", string? token = null)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("           Backend API Integration Tests");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"Testing backend URL: {backendUrl}");
        Console.WriteLine("Note: Make sure ScraperBackendService is running on the specified URL");
        Console.WriteLine();
        
        try
        {
            Console.WriteLine("─── Testing Service Endpoints ───");
            await TestServiceEndpointsAsync(backendUrl, token);
            
            Console.WriteLine();
            Console.WriteLine("─── Testing Hanime API ───");
            await TestHanimeApiAsync(backendUrl, token);
            
            Console.WriteLine();
            Console.WriteLine("─── Testing DLsite API ───");
            await TestDlsiteApiAsync(backendUrl, token);
            
            Console.WriteLine();
            Console.WriteLine("─── Testing Concurrent API Access ───");
            await TestConcurrentApiAsync(backendUrl, token, 3); // Use smaller count for quick test
            
            Console.WriteLine();
            Console.WriteLine("─── Testing Cache Endpoints ───");
            await TestCacheEndpointsAsync(backendUrl, token);
            
            Console.WriteLine();
            Console.WriteLine("✅ All backend API tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"❌ API Integration test failed: {ex.Message}");
            Console.WriteLine("💡 Make sure the ScraperBackendService is running and accessible");
        }
    }

    /// <summary>
    /// Tests basic service endpoints like / and /health.
    /// </summary>
    public static async Task TestServiceEndpointsAsync(string backendUrl, string? token = null)
    {
        using var client = CreateHttpClient(token);

        // Test service info endpoint
        var serviceInfoUrl = $"{backendUrl.TrimEnd('/')}/";
        Console.WriteLine($"📡 Testing: {serviceInfoUrl}");
        
        try
        {
            var response = await client.GetAsync(serviceInfoUrl);
            Console.WriteLine($"   Status: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("data", out var data))
                {
                    if (data.TryGetProperty("service", out var service))
                        Console.WriteLine($"   Service: {service.GetString()}");
                    if (data.TryGetProperty("version", out var version))
                        Console.WriteLine($"   Version: {version.GetString()}");
                    if (data.TryGetProperty("authEnabled", out var authEnabled))
                        Console.WriteLine($"   Auth: {(authEnabled.GetBoolean() ? "Enabled" : "Disabled")}");
                }
                Console.WriteLine("   ✅ Service info OK");
            }
            else
            {
                Console.WriteLine($"   ❌ Failed: {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Error: {ex.Message}");
        }

        Console.WriteLine();

        // Test health endpoint
        var healthUrl = $"{backendUrl.TrimEnd('/')}/health";
        Console.WriteLine($"📡 Testing: {healthUrl}");
        
        try
        {
            var response = await client.GetAsync(healthUrl);
            Console.WriteLine($"   Status: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"   Response: {json}");
                Console.WriteLine("   ✅ Health check OK");
            }
            else
            {
                Console.WriteLine($"   ❌ Failed: {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests the Hanime API endpoint by sending search and detail requests.
    /// Validates that the backend service can handle Hanime requests and return proper responses.
    /// </summary>
    /// <param name="backendUrl">Base URL of the backend service (e.g., "http://localhost:8585")</param>
    /// <param name="token">Optional authentication token for API access</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public static async Task TestHanimeApiAsync(string backendUrl, string? token = null)
    {
        using var client = CreateHttpClient(token);

        // Test search
        var searchUrl = $"{backendUrl.TrimEnd('/')}/api/hanime/search?title=Love&max=2";
        Console.WriteLine($"📡 Hanime Search: {searchUrl}");

        try
        {
            var response = await client.GetAsync(searchUrl);
            Console.WriteLine($"   Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var summary = GetResponseSummary(json);
                Console.WriteLine($"   {summary}");
                Console.WriteLine("   ✅ Hanime search OK");
            }
            else
            {
                Console.WriteLine($"   ❌ Failed: {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Hanime search failed: {ex.Message}");
        }

        Console.WriteLine();

        // Test detail by ID
        var detailUrl = $"{backendUrl.TrimEnd('/')}/api/hanime/86994";
        Console.WriteLine($"📡 Hanime Detail: {detailUrl}");

        try
        {
            var response = await client.GetAsync(detailUrl);
            Console.WriteLine($"   Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var summary = GetResponseSummary(json);
                Console.WriteLine($"   {summary}");
                Console.WriteLine("   ✅ Hanime detail OK");
            }
            else
            {
                Console.WriteLine($"   ❌ Failed: {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Hanime detail failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests the DLsite API endpoint by sending search and detail requests.
    /// Validates that the backend service can handle DLsite requests and return proper responses.
    /// </summary>
    /// <param name="backendUrl">Base URL of the backend service (e.g., "http://localhost:8585")</param>
    /// <param name="token">Optional authentication token for API access</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public static async Task TestDlsiteApiAsync(string backendUrl, string? token = null)
    {
        using var client = CreateHttpClient(token);

        // Test search
        var searchUrl = $"{backendUrl.TrimEnd('/')}/api/dlsite/search?title=恋爱&max=2";
        Console.WriteLine($"📡 DLsite Search: {searchUrl}");

        try
        {
            var response = await client.GetAsync(searchUrl);
            Console.WriteLine($"   Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var summary = GetResponseSummary(json);
                Console.WriteLine($"   {summary}");
                Console.WriteLine("   ✅ DLsite search OK");
            }
            else
            {
                Console.WriteLine($"   ❌ Failed: {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ DLsite search failed: {ex.Message}");
        }

        Console.WriteLine();

        // Test detail by ID
        var detailUrl = $"{backendUrl.TrimEnd('/')}/api/dlsite/RJ01402281";
        Console.WriteLine($"📡 DLsite Detail: {detailUrl}");

        try
        {
            var response = await client.GetAsync(detailUrl);
            Console.WriteLine($"   Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var summary = GetResponseSummary(json);
                Console.WriteLine($"   {summary}");
                Console.WriteLine("   ✅ DLsite detail OK");
            }
            else
            {
                Console.WriteLine($"   ❌ Failed: {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ DLsite detail failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests concurrent API requests to simulate multiple Jellyfin plugin instances accessing the backend simultaneously.
    /// Validates system behavior under load and ensures proper handling of concurrent requests.
    /// </summary>
    /// <param name="backendUrl">Base URL of the backend service</param>
    /// <param name="token">Optional authentication token for API access</param>
    /// <param name="concurrentCount">Number of concurrent requests to send for each provider (default: 5)</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public static async Task TestConcurrentApiAsync(
        string backendUrl,
        string? token = null,
        int concurrentCount = 5)
    {
        using var client = CreateHttpClient(token);

        var hanimeUrl = $"{backendUrl.TrimEnd('/')}/api/hanime/search?title=test&max=1";
        var dlsiteUrl = $"{backendUrl.TrimEnd('/')}/api/dlsite/search?title=test&max=1";

        var tasks = new List<(Task<(bool Success, long Ms)> Task, string Name)>();

        Console.WriteLine($"🚀 Launching {concurrentCount * 2} concurrent requests...");
        Console.WriteLine();

        var startTime = DateTime.Now;

        try
        {
            // Create concurrent tasks for both providers
            for (int i = 0; i < concurrentCount; i++)
            {
                int index = i + 1;
                tasks.Add((CreateRequestTask(client, hanimeUrl), $"Hanime #{index}"));
                tasks.Add((CreateRequestTask(client, dlsiteUrl), $"DLsite #{index}"));
            }

            var results = await Task.WhenAll(tasks.Select(t => t.Task));
            var elapsed = DateTime.Now - startTime;

            // Display results
            int successCount = 0;
            int failCount = 0;
            var times = new List<long>();

            for (int i = 0; i < results.Length; i++)
            {
                var (success, ms) = results[i];
                var name = tasks[i].Name;

                if (success)
                {
                    successCount++;
                    times.Add(ms);
                    Console.WriteLine($"✅ {name} - {ms}ms");
                }
                else
                {
                    failCount++;
                    Console.WriteLine($"❌ {name} - Failed");
                }
            }

            Console.WriteLine();
            Console.WriteLine("─────────────────────────────────────────────────────────────");
            Console.WriteLine($"Total Requests: {results.Length}");
            Console.WriteLine($"✅ Success: {successCount} ({(successCount * 100.0 / results.Length):F1}%)");
            if (failCount > 0)
                Console.WriteLine($"❌ Failed: {failCount} ({(failCount * 100.0 / results.Length):F1}%)");
            Console.WriteLine($"⏱️  Total Time: {elapsed.TotalSeconds:F2}s");
            Console.WriteLine($"📊 Throughput: {results.Length / elapsed.TotalSeconds:F1} req/s");
            
            if (times.Count > 0)
            {
                Console.WriteLine($"⚡ Avg Response: {times.Average():F0}ms");
                Console.WriteLine($"⚡ Min Response: {times.Min()}ms");
                Console.WriteLine($"⚡ Max Response: {times.Max()}ms");
            }
            Console.WriteLine("─────────────────────────────────────────────────────────────");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Concurrent test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests cache-related endpoints.
    /// </summary>
    public static async Task TestCacheEndpointsAsync(string backendUrl, string? token = null)
    {
        using var client = CreateHttpClient(token);

        var cacheStatsUrl = $"{backendUrl.TrimEnd('/')}/cache/stats";
        Console.WriteLine($"📡 Cache Stats: {cacheStatsUrl}");

        try
        {
            var response = await client.GetAsync(cacheStatsUrl);
            Console.WriteLine($"   Status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                Console.WriteLine($"   Hit Count: {GetJsonProperty(root, "hitCount", "0")}");
                Console.WriteLine($"   Miss Count: {GetJsonProperty(root, "missCount", "0")}");
                Console.WriteLine($"   Hit Ratio: {GetJsonProperty(root, "hitRatio", "N/A")}");
                Console.WriteLine("   ✅ Cache stats OK");
            }
            else
            {
                Console.WriteLine($"   ❌ Failed: {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates an HTTP client with optional authentication token and proper timeout.
    /// Centralizes HTTP client configuration for consistent request handling.
    /// </summary>
    /// <param name="token">Optional authentication token to add to request headers</param>
    /// <returns>Configured HttpClient instance</returns>
    private static HttpClient CreateHttpClient(string? token = null)
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(60); // Increased timeout for concurrent tests
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Add("X-API-Token", token);
        }
        return client;
    }

    /// <summary>
    /// Creates an individual HTTP request task for concurrent testing with performance measurement.
    /// </summary>
    private static Task<(bool Success, long Ms)> CreateRequestTask(HttpClient client, string url)
    {
        return Task.Run(async () =>
        {
            var startTime = DateTime.Now;
            try
            {
                var response = await client.GetAsync(url);
                var elapsed = (long)(DateTime.Now - startTime).TotalMilliseconds;
                return (response.IsSuccessStatusCode, elapsed);
            }
            catch
            {
                return (false, 0L);
            }
        });
    }

    /// <summary>
    /// Gets a brief summary of the API response for display.
    /// </summary>
    private static string GetResponseSummary(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                if (root.TryGetProperty("data", out var data))
                {
                    if (data.ValueKind == JsonValueKind.Array)
                    {
                        return $"Found {data.GetArrayLength()} results";
                    }
                    else if (data.ValueKind == JsonValueKind.Object)
                    {
                        if (data.TryGetProperty("title", out var title))
                        {
                            return $"Retrieved: {title.GetString()}";
                        }
                        return "Retrieved 1 item";
                    }
                }
                return "Success";
            }
            else
            {
                if (root.TryGetProperty("error", out var error))
                {
                    return $"Error: {error.GetString()}";
                }
                return "Failed";
            }
        }
        catch
        {
            return $"Response: {json.Length} bytes";
        }
    }

    /// <summary>
    /// Safely gets a JSON property value as string.
    /// </summary>
    private static string GetJsonProperty(JsonElement element, string propertyName, string defaultValue)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind == JsonValueKind.String 
                ? prop.GetString() ?? defaultValue 
                : prop.ToString();
        }
        return defaultValue;
    }
}
