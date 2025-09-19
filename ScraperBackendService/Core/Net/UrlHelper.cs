namespace ScraperBackendService.Core.Net;

/// <summary>
/// URL manipulation utilities for resolving relative URLs and comparing URL strings.
/// Provides helper methods for common URL operations in web scraping scenarios.
/// </summary>
/// <example>
/// Usage examples:
/// 
/// // Convert relative URL to absolute
/// var absoluteUrl = UrlHelper.Abs("/path/to/page", "https://example.com");
/// // Returns: "https://example.com/path/to/page"
/// 
/// // Handle protocol-relative URLs
/// var protocolUrl = UrlHelper.Abs("//cdn.example.com/image.jpg", "https://example.com");
/// // Returns: "https://cdn.example.com/image.jpg"
/// 
/// // Compare URLs for equality
/// var areEqual = UrlHelper.Eq("https://example.com/PAGE", "https://example.com/page");
/// // Returns: true (case-insensitive comparison)
/// </example>
public static class UrlHelper
{
    /// <summary>
    /// Converts a potentially relative URL to an absolute URL using the provided base URL.
    /// Handles various URL formats including relative paths, protocol-relative URLs, and absolute URLs.
    /// </summary>
    /// <param name="possiblyRelative">URL that may be relative, absolute, or protocol-relative</param>
    /// <param name="baseUrl">Base URL to resolve relative URLs against</param>
    /// <returns>Absolute URL string, or original input if conversion fails</returns>
    /// <example>
    /// // Relative path resolution
    /// var url1 = UrlHelper.Abs("/api/data", "https://example.com/page");
    /// // Returns: "https://example.com/api/data"
    /// 
    /// // Protocol-relative URL resolution
    /// var url2 = UrlHelper.Abs("//cdn.example.com/image.jpg", "https://example.com");
    /// // Returns: "https://cdn.example.com/image.jpg"
    /// 
    /// // Already absolute URL (unchanged)
    /// var url3 = UrlHelper.Abs("https://other.com/page", "https://example.com");
    /// // Returns: "https://other.com/page"
    /// 
    /// // Relative path with subdirectory
    /// var url4 = UrlHelper.Abs("../images/photo.jpg", "https://example.com/articles/2024/");
    /// // Returns: "https://example.com/articles/images/photo.jpg"
    /// 
    /// // Empty or invalid input handling
    /// var url5 = UrlHelper.Abs("", "https://example.com");
    /// // Returns: ""
    /// </example>
    public static string Abs(string possiblyRelative, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(possiblyRelative)) return "";
        if (possiblyRelative.StartsWith("//")) return "https:" + possiblyRelative;
        if (Uri.TryCreate(possiblyRelative, UriKind.Absolute, out var abs)) return abs.ToString();
        if (Uri.TryCreate(new Uri(baseUrl), possiblyRelative, out var rel)) return rel.ToString();
        return possiblyRelative;
    }

    /// <summary>
    /// Compares two URL strings for equality using case-insensitive comparison.
    /// Handles null, empty, and whitespace-only inputs gracefully.
    /// </summary>
    /// <param name="a">First URL to compare</param>
    /// <param name="b">Second URL to compare</param>
    /// <returns>True if URLs are equivalent (ignoring case), false otherwise</returns>
    /// <example>
    /// // Case-insensitive comparison
    /// var equal1 = UrlHelper.Eq("https://Example.Com/Page", "https://example.com/page");
    /// // Returns: true
    /// 
    /// // Trimming whitespace
    /// var equal2 = UrlHelper.Eq("  https://example.com  ", "https://example.com");
    /// // Returns: true
    /// 
    /// // Different URLs
    /// var equal3 = UrlHelper.Eq("https://example.com/page1", "https://example.com/page2");
    /// // Returns: false
    /// 
    /// // Null/empty handling
    /// var equal4 = UrlHelper.Eq(null, "https://example.com");
    /// // Returns: false
    /// 
    /// var equal5 = UrlHelper.Eq("", "");
    /// // Returns: false
    /// 
    /// // Image URL deduplication use case
    /// var primaryImage = "https://site.com/Cover.JPG";
    /// var thumbnailImage = "https://site.com/cover.jpg";
    /// var isDuplicate = UrlHelper.Eq(primaryImage, thumbnailImage);
    /// // Returns: true (helps avoid duplicate images in metadata)
    /// </example>
    public static bool Eq(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}