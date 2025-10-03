using ScraperBackendService.Core.Util;
using ScraperBackendService.Models;

namespace ScraperBackendService.Core.Normalize;

public static class ImageUrlNormalizer
{
    /// <summary>
    /// Select best JPG image URL from src/srcset
    /// </summary>
    public static string PickJpg(string? src, string? srcset, string baseUrl)
        => ScrapingUtils.PickJpg(src, srcset, baseUrl);

    /// <summary>
    /// Add thumbnail to metadata, automatically convert WebP to JPG
    /// </summary>
    public static void AddThumb(Metadata meta, string? url)
        => ScrapingUtils.AddThumb(meta, url);

    /// <summary>
    /// URL equality comparison
    /// </summary>
    public static bool UrlEq(string? a, string? b)
        => ScrapingUtils.UrlEq(a, b);
}
