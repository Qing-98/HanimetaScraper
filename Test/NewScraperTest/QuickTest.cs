using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Test.NewScraperTest;

/// <summary>
/// Simplified quick test class for HTTP API testing.
/// Provides lightweight testing methods for rapid development and debugging.
/// All tests use HTTP API instead of direct backend code instantiation.
/// </summary>
/// <example>
/// Usage examples:
/// - await QuickTest.TestDLsiteOnly("http://localhost:8585");
/// - await QuickTest.TestHanimeOnly("http://localhost:8585");
/// - await QuickTest.TestBothProviders("http://localhost:8585");
/// </example>
public static class QuickTest
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    /// <summary>
    /// Performs a quick test of the DLsite API.
    /// Tests search functionality with Japanese keywords via HTTP request.
    /// </summary>
    /// <param name="backendUrl">Backend service URL (e.g., "http://localhost:8585")</param>
    /// <param name="token">Optional API token for authentication</param>
    /// <returns>Task representing the asynchronous operation</returns>
    /// <example>
    /// // Run DLsite-only test
    /// await QuickTest.TestDLsiteOnly("http://localhost:8585");
    ///
    /// Expected output:
    /// - Search results for "恋爱" (love in Japanese)
    /// - Product IDs, titles, descriptions, and genre tags
    /// - Limited to 2 results for quick testing
    /// </example>
    public static async Task TestDLsiteOnly(string backendUrl = "http://localhost:8585", string? token = null)
    {
        Console.WriteLine("═══ DLsite Quick Test ═══");
        Console.WriteLine();

        ConfigureHttpClient(token);

        // Test search with Japanese keyword
        var searchUrl = $"{backendUrl.TrimEnd('/')}/api/dlsite/search?title=恋爱&max=2";
        Console.WriteLine($"📡 Testing: {searchUrl}");
        Console.WriteLine();

        try
        {
            var response = await _httpClient.GetAsync(searchUrl);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                DisplayResults(content, "DLsite");
            }
            else
            {
                Console.WriteLine($"❌ Request failed: {response.StatusCode}");
                Console.WriteLine($"   {content}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine("   Make sure ScraperBackendService is running");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Performs a quick test of the Hanime API.
    /// Tests search functionality with English keywords via HTTP request.
    /// </summary>
    /// <param name="backendUrl">Backend service URL (e.g., "http://localhost:8585")</param>
    /// <param name="token">Optional API token for authentication</param>
    /// <returns>Task representing the asynchronous operation</returns>
    /// <example>
    /// // Run Hanime-only test
    /// await QuickTest.TestHanimeOnly("http://localhost:8585");
    ///
    /// Expected output:
    /// - Search results for "Love" keyword
    /// - Video IDs, titles, descriptions, and genre tags
    /// - Limited to 2 results for quick testing
    /// </example>
    public static async Task TestHanimeOnly(string backendUrl = "http://localhost:8585", string? token = null)
    {
        Console.WriteLine("═══ Hanime Quick Test ═══");
        Console.WriteLine();

        ConfigureHttpClient(token);

        // Test search with English keyword
        var searchUrl = $"{backendUrl.TrimEnd('/')}/api/hanime/search?title=Love&max=2";
        Console.WriteLine($"📡 Testing: {searchUrl}");
        Console.WriteLine();

        try
        {
            var response = await _httpClient.GetAsync(searchUrl);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                DisplayResults(content, "Hanime");
            }
            else
            {
                Console.WriteLine($"❌ Request failed: {response.StatusCode}");
                Console.WriteLine($"   {content}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine("   Make sure ScraperBackendService is running");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Performs quick tests on both DLsite and Hanime APIs.
    /// Convenient method for testing both providers at once.
    /// </summary>
    /// <param name="backendUrl">Backend service URL (e.g., "http://localhost:8585")</param>
    /// <param name="token">Optional API token for authentication</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public static async Task TestBothProviders(string backendUrl = "http://localhost:8585", string? token = null)
    {
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("     Quick Test - Both Providers");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        await TestDLsiteOnly(backendUrl, token);
        await Task.Delay(1000);
        
        await TestHanimeOnly(backendUrl, token);
        
        Console.WriteLine("✅ Both providers tested!");
    }

    /// <summary>
    /// Configures HTTP client with optional authentication token.
    /// </summary>
    private static void ConfigureHttpClient(string? token)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Token", token);
        }
    }

    /// <summary>
    /// Displays search results in a formatted, user-friendly manner.
    /// Parses JSON response and shows result count and individual item details.
    /// </summary>
    /// <param name="jsonContent">JSON response content from API</param>
    /// <param name="providerName">Name of the provider for display purposes</param>
    private static void DisplayResults(string jsonContent, string providerName)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            // Check if it's a successful response
            if (root.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    var count = data.GetArrayLength();
                    Console.WriteLine($"✅ Found {count} results from {providerName}");
                    Console.WriteLine();

                    if (count == 0)
                    {
                        Console.WriteLine("⚠️  No results found");
                        return;
                    }

                    int index = 1;
                    foreach (var item in data.EnumerateArray())
                    {
                        Console.WriteLine($"[{index}] ─────────────────────────────");
                        
                        // Display ID
                        if (item.TryGetProperty("id", out var id))
                        {
                            Console.WriteLine($"ID: {id.GetString()}");
                        }

                        // Display Title
                        if (item.TryGetProperty("title", out var title))
                        {
                            Console.WriteLine($"Title: {title.GetString()}");
                        }

                        // Display Description (truncated)
                        if (item.TryGetProperty("description", out var description))
                        {
                            var desc = description.GetString() ?? "";
                            if (desc.Length > 80)
                            {
                                desc = desc[..80] + "...";
                            }
                            if (!string.IsNullOrWhiteSpace(desc))
                            {
                                Console.WriteLine($"Description: {desc}");
                            }
                        }

                        // Display Tags
                        if (item.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                        {
                            var tagList = new System.Collections.Generic.List<string>();
                            foreach (var tag in tags.EnumerateArray())
                            {
                                var tagStr = tag.GetString();
                                if (!string.IsNullOrWhiteSpace(tagStr))
                                {
                                    tagList.Add(tagStr);
                                }
                            }
                            if (tagList.Count > 0)
                            {
                                Console.WriteLine($"Tags: {string.Join(", ", tagList)}");
                            }
                        }

                        // Display Rating (if available)
                        if (item.TryGetProperty("rating", out var rating))
                        {
                            Console.WriteLine($"Rating: {rating.GetDouble():F1}");
                        }

                        // Display Year (if available)
                        if (item.TryGetProperty("year", out var year))
                        {
                            Console.WriteLine($"Year: {year.GetInt32()}");
                        }

                        Console.WriteLine();
                        index++;
                    }
                }
                else
                {
                    // Single item response (detail query)
                    Console.WriteLine($"✅ Retrieved {providerName} item details");
                    Console.WriteLine();
                    
                    var prettyJson = JsonSerializer.Serialize(
                        data, 
                        new JsonSerializerOptions { WriteIndented = true }
                    );
                    
                    Console.WriteLine(prettyJson.Length > 1000 
                        ? prettyJson[..1000] + "..." 
                        : prettyJson);
                }
            }
            else
            {
                // Error response
                if (root.TryGetProperty("error", out var error))
                {
                    Console.WriteLine($"❌ API Error: {error.GetString()}");
                }
                if (root.TryGetProperty("message", out var message))
                {
                    Console.WriteLine($"   {message.GetString()}");
                }
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"❌ Failed to parse JSON response: {ex.Message}");
            Console.WriteLine($"Raw content: {jsonContent[..Math.Min(500, jsonContent.Length)]}");
        }
    }
}
