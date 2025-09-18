namespace ScraperBackendService.Core.Normalize;

public static class UrlHelper
{
    public static string Abs(string maybeRelative, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(maybeRelative)) return "";
        if (maybeRelative.StartsWith("//")) return "https:" + maybeRelative;
        if (Uri.TryCreate(maybeRelative, UriKind.Absolute, out var abs)) return abs.ToString();
        if (Uri.TryCreate(new Uri(baseUrl), maybeRelative, out var rel)) return rel.ToString();
        return maybeRelative;
    }

    public static bool Eq(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
           && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
