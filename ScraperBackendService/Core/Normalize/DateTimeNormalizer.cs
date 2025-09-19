using System.Text.RegularExpressions;
using System.Globalization;
using ScraperBackendService.Core.Util;

namespace ScraperBackendService.Core.Normalize;

public static class DateTimeNormalizer
{
    /// <summary>
    /// Parse Japanese date format: yyyy年M月d日
    /// </summary>
    public static DateTimeOffset? ParseJapaneseYmd(string? s)
        => ScrapingUtils.ParseJapaneseDate(s);

    public static DateTimeOffset? ParseIsoYmd(string? s)
        => DateTimeOffset.TryParse(s, out var dt) ? dt : null;

    public static TimeSpan? ParseDuration(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (TimeSpan.TryParse(s, out var ts)) return ts;        // "24:00"
        var m = Regex.Match(s, @"(\d{1,3})\s*min", RegexOptions.IgnoreCase);
        return m.Success ? TimeSpan.FromMinutes(int.Parse(m.Groups[1].Value)) : null;
    }
}
