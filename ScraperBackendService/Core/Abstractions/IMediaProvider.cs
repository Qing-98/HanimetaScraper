using ScraperBackendService.Models;
using System.Text.Json;

namespace ScraperBackendService.Core.Abstractions;

/// <summary>
/// Site-agnostic media provider abstraction for content scraping operations.
/// Defines the core contract for content discovery and metadata extraction.
/// </summary>
/// <example>
/// Implementation example:
/// public class MyProvider : IMediaProvider
/// {
///     public string Name => "MyProvider";
///
///     public bool TryParseId(string input, out string id)
///     {
///         // Parse provider-specific ID from input
///         return IdParsers.TryParseMyProviderId(input, out id);
///     }
///
///     public string BuildDetailUrlById(string id)
///     {
///         return $"https://myprovider.com/content/{id}";
///     }
///
///     public async Task&lt;IReadOnlyList&lt;SearchHit&gt;&gt; SearchAsync(string keyword, int maxResults, CancellationToken ct)
///     {
///         // Implement search logic
///         var results = await PerformSearch(keyword, maxResults, ct);
///         return results.Select(r => new SearchHit(r.Url, r.Title, r.CoverUrl)).ToList();
///     }
///
///     public async Task&lt;HanimeMetadata?&gt; FetchDetailAsync(string detailUrl, CancellationToken ct)
///     {
///         // Implement detail extraction logic
///         var html = await _networkClient.GetHtmlAsync(detailUrl, ct);
///         return ParseMetadata(html, detailUrl);
///     }
/// }
///
/// Usage example:
/// var provider = new MyProvider(networkClient, logger);
/// var searchResults = await provider.SearchAsync("keyword", 10, ct);
/// var details = await provider.FetchDetailAsync(searchResults[0].DetailUrl, ct);
/// </example>
public interface IMediaProvider
{
    /// <summary>
    /// Provider name identifier for logging and identification purposes.
    /// </summary>
    /// <example>
    /// "Hanime", "DLsite", "MyCustomProvider"
    /// </example>
    string Name { get; }

    /// <summary>
    /// Attempts to parse a provider-specific ID from the given input string.
    /// Supports URLs, direct IDs, and various input formats.
    /// </summary>
    /// <param name="input">Input string that may contain a provider ID</param>
    /// <param name="id">Extracted ID if parsing succeeds</param>
    /// <returns>True if ID was successfully parsed, false otherwise</returns>
    /// <example>
    /// // Parse from URL
    /// provider.TryParseId("https://site.com/watch?v=12345", out var id); // id = "12345"
    ///
    /// // Parse from direct ID
    /// provider.TryParseId("RJ123456", out var id); // id = "RJ123456"
    ///
    /// // Invalid input
    /// provider.TryParseId("invalid", out var id); // returns false
    /// </example>
    bool TryParseId(string input, out string id);

    /// <summary>
    /// Builds a detail page URL from a provider-specific ID.
    /// Constructs the canonical URL for accessing content details.
    /// </summary>
    /// <param name="id">Provider-specific content ID</param>
    /// <returns>Complete URL to the content detail page</returns>
    /// <example>
    /// var url = provider.BuildDetailUrlById("12345");
    /// // Returns: "https://provider.com/content/12345"
    ///
    /// var url2 = provider.BuildDetailUrlById("RJ123456");
    /// // Returns: "https://dlsite.com/maniax/work/=/product_id/RJ123456.html"
    /// </example>
    string BuildDetailUrlById(string id);

    /// <summary>
    /// Searches for content using the provided keyword.
    /// Returns a list of search hits with basic information for further processing.
    /// </summary>
    /// <param name="keyword">Search keyword or phrase (supports various languages)</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <param name="ct">Cancellation token for operation timeout</param>
    /// <returns>Read-only list of search hits containing URLs, titles, and cover images</returns>
    /// <example>
    /// // Basic search
    /// var results = await provider.SearchAsync("Love", 10, CancellationToken.None);
    /// foreach (var hit in results)
    /// {
    ///     Console.WriteLine($"Found: {hit.Title} at {hit.DetailUrl}");
    ///     if (!string.IsNullOrEmpty(hit.CoverUrl))
    ///         Console.WriteLine($"Cover: {hit.CoverUrl}");
    /// }
    ///
    /// // Search with timeout
    /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    /// var results2 = await provider.SearchAsync("Romance", 5, cts.Token);
    /// </example>
    Task<IReadOnlyList<SearchHit>> SearchAsync(
        string keyword, int maxResults, CancellationToken ct);

    /// <summary>
    /// Fetches detailed metadata for a specific content URL.
    /// Extracts comprehensive information including title, description, ratings, images, and personnel.
    /// </summary>
    /// <param name="detailUrl">URL of the detail page to scrape</param>
    /// <param name="ct">Cancellation token for operation timeout</param>
    /// <returns>Complete metadata object or null if extraction fails</returns>
    /// <example>
    /// var metadata = await provider.FetchDetailAsync("https://site.com/content/12345", CancellationToken.None);
    /// if (metadata != null)
    /// {
    ///     Console.WriteLine($"Title: {metadata.Title}");
    ///     Console.WriteLine($"Description: {metadata.Description}");
    ///     Console.WriteLine($"Rating: {metadata.Rating}/5");
    ///     Console.WriteLine($"Release Date: {metadata.ReleaseDate}");
    ///     Console.WriteLine($"Studios: {string.Join(", ", metadata.Studios)}");
    ///     Console.WriteLine($"Genres: {string.Join(", ", metadata.Genres)}");
    ///     Console.WriteLine($"People: {metadata.People.Count} entries");
    ///     Console.WriteLine($"Images: Primary={metadata.Primary}, Backdrop={metadata.Backdrop}, Thumbnails={metadata.Thumbnails.Count}");
    /// }
    /// </example>
    Task<HanimeMetadata?> FetchDetailAsync(
        string detailUrl, CancellationToken ct);
}

/// <summary>
/// Search hit data transfer object that bridges search and detail extraction layers.
/// Contains minimal information needed to identify and access content for detailed extraction.
/// </summary>
/// <param name="DetailUrl">Complete URL to the content detail page</param>
/// <param name="Title">Content title (may be null if not available during search)</param>
/// <param name="CoverUrl">Cover image URL (may be null if not available during search)</param>
/// <example>
/// // Create search hit from search results
/// var hit = new SearchHit("https://site.com/content/12345", "Example Title", "https://site.com/covers/12345.jpg");
///
/// // Create minimal search hit (title and cover determined later)
/// var hit2 = new SearchHit("https://site.com/content/67890", null, null);
///
/// // Use search hit for detail extraction
/// var metadata = await provider.FetchDetailAsync(hit.DetailUrl, ct);
///
/// // Search hits are immutable records
/// var newHit = hit with { Title = "Updated Title" };
/// </example>
public sealed record SearchHit(string DetailUrl, string? Title, string? CoverUrl);
