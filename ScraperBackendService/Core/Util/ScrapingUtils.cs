using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ScraperBackendService.Models;

namespace ScraperBackendService.Core.Util;

/// <summary>
/// Unified scraping utility class to eliminate duplicate code across providers
/// </summary>
public static class ScrapingUtils
{
    private static readonly Regex SpaceCollapseRe = new(@"\s+", RegexOptions.Compiled);

    // ================= Text Processing Tools =================

    /// <summary>
    /// Build search keywords from filename, removing quality/encoding tags
    /// </summary>
    public static string BuildQueryFromFilename(string filenameOrText)
    {
        if (string.IsNullOrWhiteSpace(filenameOrText)) return "";

        var name = Path.GetFileNameWithoutExtension(filenameOrText.Trim());

        // Remove common quality/encoding/audio track tags
        var cleaned = Regex.Replace(name,
            @"(?i)\b(1080p|2160p|720p|480p|hevc|x26[45]|h\.?26[45]|aac|flac|hdr|dv|10bit|8bit|webrip|web-dl|bluray|remux|sub|chs|cht|eng|multi|unrated|proper|repack)\b",
            " ");

        // Remove brackets
        cleaned = Regex.Replace(cleaned, @"[\[\]\(\)\{\}【】（）]", " ");
        // Replace underscores/dots with spaces
        cleaned = Regex.Replace(cleaned, @"[_\.]+", " ");

        return SpaceCollapseRe.Replace(cleaned, " ").Trim();
    }

    /// <summary>
    /// Keyword normalization: keep letters/digits/spaces/underscores/hyphens, replace others with spaces, convert spaces to +
    /// </summary>
    public static string NormalizeKeyword(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";

        var arr = s.Trim().ToCharArray();
        for (int i = 0; i < arr.Length; i++)
        {
            var r = arr[i];
            if (char.IsLetterOrDigit(r) || char.IsWhiteSpace(r) || r == '_' || r == '-') continue;
            arr[i] = ' ';
        }
        var cleaned = new string(arr);
        return SpaceCollapseRe.Replace(cleaned, " ").Trim().Replace(' ', '+');
    }

    /// <summary>
    /// General text cleaning: decode HTML, replace full-width spaces, compress extra spaces
    /// </summary>
    public static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = WebUtility.HtmlDecode(s).Replace('　', ' ').Trim();
        return SpaceCollapseRe.Replace(s, " ");
    }

    /// <summary>
    /// Clean tag text: remove quotes, colons, non-breaking spaces, etc.
    /// </summary>
    public static string CleanTag(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return raw.Replace("\"", "")
                  .Replace('\u201C'.ToString(), "") // Left double quote
                  .Replace('\u201D'.ToString(), "") // Right double quote
                  .Replace("：", "")
                  .Replace(":", "")
                  .Replace("\u00A0", "")
                  .Replace("&nbsp;", "")
                  .Trim();
    }

    // ================= HTML Parsing Tools =================

    /// <summary>
    /// Select single HTML node
    /// </summary>
    public static HtmlNode? SelectSingle(HtmlDocument doc, string xpath)
        => doc.DocumentNode.SelectSingleNode(xpath);

    /// <summary>
    /// Select multiple HTML nodes
    /// </summary>
    public static HtmlNodeCollection? SelectNodes(HtmlDocument doc, string xpath)
        => doc.DocumentNode.SelectNodes(xpath);

    /// <summary>
    /// Select node and get text content
    /// </summary>
    public static string SelectText(HtmlDocument doc, string xpath)
        => doc.DocumentNode.SelectSingleNode(xpath)?.InnerText?.Trim() ?? "";

    /// <summary>
    /// Get node attribute value
    /// </summary>
    public static string GetAttr(HtmlNode? node, string name)
        => node?.GetAttributeValue(name, "") ?? "";

    /// <summary>
    /// Convert relative URL to absolute URL
    /// </summary>
    public static string AbsUrl(string possiblyRelative, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(possiblyRelative)) return "";
        if (possiblyRelative.StartsWith("//")) return "https:" + possiblyRelative;
        if (Uri.TryCreate(possiblyRelative, UriKind.Absolute, out var abs)) return abs.ToString();
        if (Uri.TryCreate(new Uri(baseUrl), possiblyRelative, out var rel)) return rel.ToString();
        return possiblyRelative;
    }

    // ================= Image Processing Tools =================

    /// <summary>
    /// Select best JPG image URL from src/srcset
    /// </summary>
    public static string PickJpg(string? src, string? srcset, string baseUrl)
    {
        string TryPick(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var abs = AbsUrl(s, baseUrl);
            if (abs.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) return abs;
            if (abs.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                var i = abs.LastIndexOf('.');
                return i > 0 ? abs[..i] + ".jpg" : abs;
            }
            return "";
        }

        var a = TryPick(src ?? "");
        if (!string.IsNullOrEmpty(a)) return a;

        foreach (var part in (srcset ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = part.Trim().Split(' ')[0];
            var m2 = TryPick(p);
            if (!string.IsNullOrEmpty(m2)) return m2;
        }
        return "";
    }

    /// <summary>
    /// Add thumbnail to metadata, automatically convert WebP to JPG
    /// </summary>
    public static void AddThumb(Metadata meta, string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        var normalized = url.Trim();
        if (normalized.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            var idx = normalized.LastIndexOf('.');
            if (idx > 0) normalized = normalized[..idx] + ".jpg";
        }

        if (!meta.Thumbnails.Contains(normalized))
            meta.Thumbnails.Add(normalized);
    }

    /// <summary>
    /// URL equality comparison
    /// </summary>
    public static bool UrlEq(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    // ================= ID Parsing Tools =================

    private static readonly Regex HanimeUrlIdRegex = new(@"(?i)https?://(?:www\.)?hanime1\.me/watch\?v=(\d{3,})", RegexOptions.Compiled);
    private static readonly Regex HanimeBareIdRegex = new(@"^\d{3,}$", RegexOptions.Compiled);
    private static readonly Regex DlsiteRjRegex = new(@"(?i)^RJ\d+$", RegexOptions.Compiled);
    private static readonly Regex DlsiteVjRegex = new(@"(?i)^VJ\d+$", RegexOptions.Compiled);
    private static readonly Regex DlsiteProductPathRegex = new(@"(?i)^(RJ|VJ)\d+\.html$", RegexOptions.Compiled);

    /// <summary>
    /// Parse Hanime numeric ID
    /// </summary>
    public static bool TryParseHanimeId(string? input, out string id)
    {
        id = "";
        if (string.IsNullOrWhiteSpace(input)) return false;

        var t = input.Trim();

        // Try to parse from URL
        var m = HanimeUrlIdRegex.Match(t);
        if (m.Success) { id = m.Groups[1].Value; return true; }

        // Try pure numeric (only accept numeric input as valid ID)
        if (HanimeBareIdRegex.IsMatch(t)) { id = t; return true; }

        // For non-numeric input, this is NOT a valid ID - should use search instead
        return false;
    }

    /// <summary>
    /// Parse DLsite ID
    /// </summary>
    public static bool TryParseDlsiteId(string? input, out string id)
    {
        id = "";
        var t = input?.Trim() ?? "";

        // Direct match RJ/VJ format
        if (DlsiteRjRegex.IsMatch(t) || DlsiteVjRegex.IsMatch(t))
        {
            id = t.ToUpperInvariant();
            return true;
        }

        // Parse from URL
        var parsed = ParseDlsiteIdFromUrl(t);
        if (!string.IsNullOrEmpty(parsed))
        {
            id = parsed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parse ID from DLsite URL
    /// </summary>
    public static string ParseDlsiteIdFromUrl(string rawUrl)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)) return "";

        var baseName = Path.GetFileName(uri.AbsolutePath);
        if (DlsiteProductPathRegex.IsMatch(baseName))
        {
            var id = baseName[..baseName.LastIndexOf('.')];
            return id.Trim().ToUpperInvariant();
        }

        foreach (var seg in uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (DlsiteRjRegex.IsMatch(seg) || DlsiteVjRegex.IsMatch(seg))
                return seg.Trim().ToUpperInvariant();
        }

        return "";
    }

    // ================= Personnel Role Mapping =================

    /// <summary>
    /// Map Japanese role names to English types
    /// </summary>
    public static (string Type, string? Role) MapStaffRole(string header)
    {
        if (string.IsNullOrEmpty(header)) return (header, null);

        // Director
        if (header.Contains("監督") || header.Contains("ディレクター"))
            return ("Director", null);

        // Writer
        if (header.Contains("シナリオ") || header.Contains("脚本"))
            return ("Writer", null);

        // Artist
        if (header.Contains("原画") || header.Contains("イラスト"))
            return ("Illustrator", null);

        // Producer
        if (header.Contains("制作") || header.Contains("企画") || header.Contains("プロデューサ"))
            return ("Producer", null);

        // Editor
        if (header.Contains("編集"))
            return ("Editor", null);

        // Music
        if (header.Contains("音楽"))
            return ("Composer", null);

        // Actor
        if (header.Contains("声優") || header.Contains("出演者") || header.Contains("キャスト"))
            return ("Actor", null);

        // Unrecognized: keep original
        return (header, null);
    }

    /// <summary>
    /// Add person to metadata with automatic deduplication.
    /// Type: normalized English role (Actor, Director, etc.)
    /// Role: original role name (声優, 監督, etc.)
    /// </summary>
    /// <param name="meta">Metadata to add person to</param>
    /// <param name="name">Person's name</param>
    /// <param name="normalizedType">Standardized English role type</param>
    /// <param name="originalRole">Original role name in source language</param>
    /// <example>
    /// // Add voice actor with Japanese role
    /// AddPerson(metadata, "田中花音", "Actor", "声優");
    ///
    /// // Add director with standardized type
    /// AddPerson(metadata, "山田監督", "Director", "監督");
    /// </example>
    public static void AddPerson(Metadata meta, string name, string normalizedType, string? originalRole)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        // Deduplicate by Name+Type (using normalized type for consistency)
        if (meta.People.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                               && p.Type.Equals(normalizedType, StringComparison.OrdinalIgnoreCase)))
            return;

        meta.People.Add(new PersonDto
        {
            Name = name,
            Type = normalizedType,        // Standardized English role type (Actor, Director, etc.)
            Role = originalRole           // Original role name (声優, 監督, etc.)
        });
    }

    // ================= Date Parsing Tools =================

    private static readonly Regex DateJpRegex = new(@"(\d{4})年(\d{1,2})月(\d{1,2})日", RegexOptions.Compiled);

    /// <summary>
    /// Parse Japanese date format: yyyy年M月d日
    /// </summary>
    public static DateTimeOffset? ParseJapaneseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = DateJpRegex.Match(text);
        if (!match.Success) return null;

        if (int.TryParse(match.Groups[1].Value, out var year) &&
            int.TryParse(match.Groups[2].Value, out var month) &&
            int.TryParse(match.Groups[3].Value, out var day))
        {
            try
            {
                return new DateTimeOffset(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    // ================= HTML Table Parsing Tools =================

    /// <summary>
    /// Extract content from table cell, prefer first link
    /// </summary>
    public static string ExtractOutlineCell(HtmlDocument doc, string xpath)
    {
        var td = doc.DocumentNode.SelectSingleNode(xpath);
        if (td == null) return "";

        var a1 = td.SelectSingleNode(".//a[1]");
        var val = Clean(a1?.InnerText ?? td.InnerText);
        return val;
    }

    /// <summary>
    /// Extract content from table cell, prefer link over text
    /// </summary>
    public static string ExtractOutlineCellPreferA(HtmlDocument doc, string xpath)
    {
        var td = doc.DocumentNode.SelectSingleNode(xpath);
        if (td == null) return "";

        var a = td.SelectSingleNode(".//a");
        return Clean(a?.InnerText ?? td.InnerText);
    }
}
