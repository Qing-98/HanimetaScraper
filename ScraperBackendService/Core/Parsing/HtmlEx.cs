using HtmlAgilityPack;
using System.Text;
using System.Net;
using System.Linq;

namespace ScraperBackendService.Core.Parsing;

public static class HtmlEx
{
    public static HtmlNode? SelectSingle(HtmlDocument d, string xp)
        => d.DocumentNode.SelectSingleNode(xp);

    public static IEnumerable<HtmlNode> SelectNodes(HtmlDocument d, string xp)
        => d.DocumentNode.SelectNodes(xp) ?? Enumerable.Empty<HtmlNode>();  // ★ 返回 IEnumerable
    public static string? GetAttrOrNull(HtmlNode? n, string name)
    => n?.Attributes[name]?.Value;

    public static string SelectText(HtmlDocument d, string xp)
        => d.DocumentNode.SelectSingleNode(xp)?.InnerText?.Trim() ?? "";

    public static string GetAttr(HtmlNode? n, string name)
        => n?.GetAttributeValue(name, "") ?? "";

    public static string ExtractOutlineCell(HtmlDocument d, string xp, Func<string, string>? cleaner = null)
    {
        var td = d.DocumentNode.SelectSingleNode(xp);
        if (td == null) return "";
        var a1 = td.SelectSingleNode(".//a[1]");
        var raw = a1?.InnerText ?? td.InnerText;
        return (cleaner?.Invoke(raw) ?? raw).Trim();
    }

    public static string ExtractOutlineCellPreferA(HtmlDocument d, string xp, Func<string, string>? cleaner = null)
    {
        var td = d.DocumentNode.SelectSingleNode(xp);
        if (td == null) return "";
        var a = td.SelectSingleNode(".//a");
        var raw = a?.InnerText ?? td.InnerText;
        return (cleaner?.Invoke(raw) ?? raw).Trim();
    }

    public static string TextWithBr(HtmlNode n)
    {
        var sb = new StringBuilder();
        void Walk(HtmlNode x)
        {
            if (x.NodeType == HtmlNodeType.Text) sb.Append(x.InnerText);
            else if (string.Equals(x.Name, "br", StringComparison.OrdinalIgnoreCase)) sb.Append('\n');
            foreach (var c in x.ChildNodes) Walk(c);
        }
        Walk(n);
        return WebUtility.HtmlDecode(sb.ToString());
    }
}
