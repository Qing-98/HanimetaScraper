using System.Text.RegularExpressions;
using System.Net;
using ScraperBackendService.Core.Util;

namespace ScraperBackendService.Core.Normalize;

public static class TextNormalizer
{
    /// <summary>
    /// Remove extra symbols/marks, suitable for constructing search keywords from filenames.
    /// </summary>
    public static string BuildQueryFromFilename(string filenameOrText)
        => ScrapingUtils.BuildQueryFromFilename(filenameOrText);

    /// <summary>
    /// Preserve letters/digits/spaces/underscores/hyphens, replace others with spaces; convert spaces to +
    /// </summary>
    public static string NormalizeKeyword(string s)
        => ScrapingUtils.NormalizeKeyword(s);

    /// <summary>
    /// General cleaning: decode HTML, replace full-width spaces, compress extra spaces.
    /// </summary>
    public static string Clean(string s)
        => ScrapingUtils.Clean(s);
}
