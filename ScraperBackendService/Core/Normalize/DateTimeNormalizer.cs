using System.Text.RegularExpressions;
using System.Globalization;

namespace ScraperBackendService.Core.Normalize;

public static class DateTimeNormalizer
{
    private static readonly Regex JpDateRe = new(@"(\d{4})年(\d{1,2})月(\d{1,2})日", RegexOptions.Compiled);

    public static DateTimeOffset? ParseJapaneseYmd(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = JpDateRe.Match(s);
        if (!m.Success) return null;
        var y = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var M = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var d = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        return new DateTimeOffset(new DateTime(y, M, d, 0, 0, 0, DateTimeKind.Utc));
    }

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
