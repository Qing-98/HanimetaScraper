using System.Text.RegularExpressions;
using System.Net;

namespace ScraperBackendService.Core.Normalize;

public static class TextNormalizer
{
    private static readonly Regex SpaceCollapseRe = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// 去掉多余的符号/标记，适合从文件名构造搜索关键词。
    /// </summary>
    public static string BuildQueryFromFilename(string filenameOrText)
    {
        if (string.IsNullOrWhiteSpace(filenameOrText)) return "";

        var name = Path.GetFileNameWithoutExtension(filenameOrText.Trim());

        // 去掉常见画质/编码/音轨标签
        var cleaned = Regex.Replace(name,
            @"(?i)\b(1080p|2160p|720p|480p|hevc|x26[45]|h\.?26[45]|aac|flac|hdr|dv|10bit|8bit|webrip|web-dl|bluray|remux|sub|chs|cht|eng|multi|unrated|proper|repack)\b",
            " ");

        // 去掉括号/中括号
        cleaned = Regex.Replace(cleaned, @"[\[\]\(\)\{\}【】（）]", " ");
        // 把下划线/点替换为空格
        cleaned = Regex.Replace(cleaned, @"[_\.]+", " ");

        return SpaceCollapseRe.Replace(cleaned, " ").Trim();
    }

    /// <summary>
    /// 保留字母/数字/空格/下划线/连字符，其他替换为空格；并把空格转为 +
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
    /// 通用清理：解码 HTML，替换全角空格，压缩多余空格。
    /// </summary>
    public static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = WebUtility.HtmlDecode(s).Replace('　', ' ').Trim();
        return SpaceCollapseRe.Replace(s, " ");
    }
}
