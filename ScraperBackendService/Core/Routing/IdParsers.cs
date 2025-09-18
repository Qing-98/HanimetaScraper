using System.Text.RegularExpressions;

namespace ScraperBackendService.Core.Routing;

public static class IdParsers
{
    // ===== Hanime =====
    private static readonly Regex HanimeUrlNumericIdRegex =
        new(@"(?i)https?://(?:www\.)?hanime1\.me/watch\?v=(\d{3,})", RegexOptions.Compiled);
    private static readonly Regex HanimeBareNumericIdRegex =
        new(@"^\d{3,}$", RegexOptions.Compiled);

    public static bool TryParseHanimeId(string? text, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();

        var m = HanimeUrlNumericIdRegex.Match(t);
        if (m.Success) { id = m.Groups[1].Value; return true; }
        if (HanimeBareNumericIdRegex.IsMatch(t)) { id = t; return true; }
        return false;
    }

    public static bool TryExtractHanimeIdFromUrl(string url, out string id)
    {
        id = "";
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u)) return false;
        if (!u.Host.EndsWith("hanime1.me", StringComparison.OrdinalIgnoreCase)) return false;
        var m = Regex.Match(u.Query, @"(?:^|[?&])v=(\d{3,})\b");
        if (!m.Success) return false;
        id = m.Groups[1].Value; return true;
    }

    public static string BuildHanimeDetailUrl(string numericId) => $"https://hanime1.me/watch?v={numericId}";

    // ===== DLsite =====
    private static readonly Regex RjRe = new(@"(?i)^RJ\d+$", RegexOptions.Compiled);
    private static readonly Regex VjRe = new(@"(?i)^VJ\d+$", RegexOptions.Compiled);
    private static readonly Regex ProductPathRe = new(@"(?i)^(RJ|VJ)\d+\.html$", RegexOptions.Compiled);

    public static bool TryParseDlsiteId(string input, out string id)
    {
        id = "";
        var t = input?.Trim() ?? "";
        if (RjRe.IsMatch(t) || VjRe.IsMatch(t)) { id = t.ToUpperInvariant(); return true; }
        var parsed = ParseIdFromDlsiteUrl(t);
        if (!string.IsNullOrEmpty(parsed)) { id = parsed; return true; }
        return false;
    }

    public static string ParseIdFromDlsiteUrl(string rawUrl)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)) return "";
        var baseName = Path.GetFileName(uri.AbsolutePath);
        if (ProductPathRe.IsMatch(baseName))
        {
            var id = baseName[..baseName.LastIndexOf('.')];
            return id.Trim().ToUpperInvariant();
        }
        foreach (var seg in uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (RjRe.IsMatch(seg) || VjRe.IsMatch(seg)) return seg.Trim().ToUpperInvariant();
        }
        return "";
    }

    public static string BuildDlsiteDetailUrl(string id, bool preferManiax = true)
        => string.Format(preferManiax
            ? "https://www.dlsite.com/maniax/work/=/product_id/{0}.html"
            : "https://www.dlsite.com/pro/work/=/product_id/{0}.html", id);
}
