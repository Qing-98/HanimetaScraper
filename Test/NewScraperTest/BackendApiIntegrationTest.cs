using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Test.NewScraperTest;

/// <summary>
/// Integration test class for testing backend API endpoints.
/// Simulates real-world scenarios where Jellyfin frontend communicates with the scraper backend service.
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
    /// <returns>Task representing the asynchronous operation</returns>
    public static async Task RunTestsAsync()
    {
        var backendUrl = "http://localhost:8585";
        
        Console.WriteLine("=== Backend API Integration Tests ===");
        Console.WriteLine($"Testing backend URL: {backendUrl}");
        Console.WriteLine("Note: Make sure ScraperBackendService is running on the specified URL");
        
        try
        {
            Console.WriteLine("\n--- Testing Hanime API ---");
            await TestHanimeApiAsync(backendUrl);
            
            Console.WriteLine("\n--- Testing DLsite API ---");
            await TestDlsiteApiAsync(backendUrl);
            
            Console.WriteLine("\n--- Testing Concurrent API Access ---");
            await TestConcurrentApiAsync(backendUrl, null, 3); // Use smaller count for quick test
            
            Console.WriteLine("\n✅ All backend API tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ API Integration test failed: {ex.Message}");
            Console.WriteLine("💡 Make sure the ScraperBackendService is running and accessible");
        }
    }

    /// <summary>
    /// Tests the Hanime API endpoint by sending a search request.
    /// Validates that the backend service can handle Hanime search requests and return proper responses.
    /// </summary>
    /// <param name="backendUrl">Base URL of the backend service (e.g., "http://localhost:8585")</param>
    /// <param name="token">Optional authentication token for API access</param>
    /// <returns>Task representing the asynchronous operation</returns>
    /// <example>
    /// // Test without authentication
    /// await TestHanimeApiAsync("http://localhost:8585");
    ///
    /// // Test with authentication token
    /// await TestHanimeApiAsync("http://localhost:8585", "abc123");
    /// </example>
    public static async Task TestHanimeApiAsync(string backendUrl, string? token = null)
    {
        using var client = CreateHttpClient(token);

        var url = $"{backendUrl.TrimEnd('/')}/api/hanime/search?title=Love&max=2";
        Console.WriteLine($"[Integration Test] Request: {url}");

        try
        {
            var response = await client.GetAsync(url);
            Console.WriteLine($"Status Code: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response Content:");
                Console.WriteLine(json.Length > 500 ? json[..500] + "..." : json);
            }
            else
            {
                Console.WriteLine("Request Failed: " + await response.Content.ReadAsStringAsync());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hanime API request failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests the DLsite API endpoint by sending a search request.
    /// Validates that the backend service can handle DLsite search requests and return proper responses.
    /// </summary>
    /// <param name="backendUrl">Base URL of the backend service (e.g., "http://localhost:8585")</param>
    /// <param name="token">Optional authentication token for API access</param>
    /// <returns>Task representing the asynchronous operation</returns>
    /// <example>
    /// // Test DLsite search with Japanese text
    /// await TestDlsiteApiAsync("http://localhost:8585");
    ///
    /// // Test with authentication
    /// await TestDlsiteApiAsync("http://localhost:8585", "your-token");
    /// </example>
    public static async Task TestDlsiteApiAsync(string backendUrl, string? token = null)
    {
        using var client = CreateHttpClient(token);

        var url = $"{backendUrl.TrimEnd('/')}/api/dlsite/search?title=恋爱&max=2";
        Console.WriteLine($"[Integration Test] Request: {url}");

        try
        {
            var response = await client.GetAsync(url);
            Console.WriteLine($"Status Code: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response Content:");
                Console.WriteLine(json.Length > 500 ? json[..500] + "..." : json);
            }
            else
            {
                Console.WriteLine("Request Failed: " + await response.Content.ReadAsStringAsync());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DLsite API request failed: {ex.Message}");
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
    /// <example>
    /// // Test with default concurrency (5 requests per provider)
    /// await TestConcurrentApiAsync("http://localhost:8585");
    ///
    /// // Test with high concurrency
    /// await TestConcurrentApiAsync("http://localhost:8585", "token", 20);
    /// </example>
    public static async Task TestConcurrentApiAsync(
        string backendUrl,
        string? token = null,
        int concurrentCount = 5)
    {
        using var client = CreateHttpClient(token);

        var hanimeUrl = $"{backendUrl.TrimEnd('/')}/api/hanime/search?title=Love&max=1";
        var dlsiteUrl = $"{backendUrl.TrimEnd('/')}/api/dlsite/search?title=恋爱&max=1";

        var tasks = new List<Task>();

        Console.WriteLine($"[Concurrent Test] Launching {concurrentCount} Hanime and {concurrentCount} DLsite requests simultaneously...");

        try
        {
            // Create concurrent tasks for both providers
            for (int i = 0; i < concurrentCount; i++)
            {
                tasks.Add(CreateRequestTask(client, hanimeUrl, "Hanime"));
                tasks.Add(CreateRequestTask(client, dlsiteUrl, "DLsite"));
            }

            await Task.WhenAll(tasks);
            Console.WriteLine("[Concurrent Test] All requests completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Concurrent test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates an HTTP client with optional authentication token.
    /// Centralizes HTTP client configuration for consistent request handling.
    /// </summary>
    /// <param name="token">Optional authentication token to add to request headers</param>
    /// <returns>Configured HttpClient instance</returns>
    /// <example>
    /// var client = CreateHttpClient("abc123"); // With token
    /// var client = CreateHttpClient(); // Without token
    /// </example>
    private static HttpClient CreateHttpClient(string? token = null)
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30); // Set reasonable timeout
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Add("X-API-Token", token);
        }
        return client;
    }

    /// <summary>
    /// Creates an individual HTTP request task for concurrent testing.
    /// Encapsulates request execution logic with proper error handling and response measurement.
    /// </summary>
    /// <param name="client">HTTP client to use for the request</param>
    /// <param name="url">URL to request</param>
    /// <param name="providerName">Name of the provider for logging purposes</param>
    /// <returns>Task representing the asynchronous HTTP request</returns>
    /// <example>
    /// var task = CreateRequestTask(client, "http://api.example.com/search", "ExampleProvider");
    /// await task;
    /// </example>
    private static Task CreateRequestTask(HttpClient client, string url, string providerName)
    {
        return Task.Run(async () =>
        {
            try
            {
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[{providerName}] Status: {response.StatusCode}, Content Length: {content.Length}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{providerName}] Error: {ex.Message}");
            }
        });
    }
}
