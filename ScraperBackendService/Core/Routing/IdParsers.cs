using System.Text.RegularExpressions;
using System.Globalization;
using ScrapingUtils = ScraperBackendService.Core.Util.ScrapingUtils;

namespace ScraperBackendService.Core.Routing;

/// <summary>
/// ID parsing and URL construction utilities
/// </summary>
public static class IdParsers
{
    // Hanime related
    public static bool TryParseHanimeId(string? input, out string id)
        => ScrapingUtils.TryParseHanimeId(input, out id);

    public static string BuildHanimeDetailUrl(string id) => $"https://hanime1.me/watch?v={id}";

    public static bool TryExtractHanimeIdFromUrl(string url, out string id)
        => ScrapingUtils.TryParseHanimeId(url, out id);

    // DLsite related
    public static bool TryParseDlsiteId(string? input, out string id)
        => ScrapingUtils.TryParseDlsiteId(input, out id);

    public static string ParseIdFromDlsiteUrl(string rawUrl)
        => ScrapingUtils.ParseDlsiteIdFromUrl(rawUrl);

    public static string BuildDlsiteDetailUrl(string id, bool preferManiax = true)
    {
        id = id.Trim().ToUpperInvariant();

        // If ID clearly indicates site by prefix, choose accordingly
        // VJ-prefixed IDs are served under the "pro" section; RJ are typically under "maniax"
        var template = preferManiax
            ? "https://www.dlsite.com/maniax/work/=/product_id/{0}.html"
            : "https://www.dlsite.com/pro/work/=/product_id/{0}.html";

        if (id.StartsWith("VJ", StringComparison.OrdinalIgnoreCase))
        {
            template = "https://www.dlsite.com/pro/work/=/product_id/{0}.html";
        }
        else if (id.StartsWith("RJ", StringComparison.OrdinalIgnoreCase))
        {
            template = "https://www.dlsite.com/maniax/work/=/product_id/{0}.html";
        }

        return string.Format(CultureInfo.InvariantCulture, template, id);
    }
}
