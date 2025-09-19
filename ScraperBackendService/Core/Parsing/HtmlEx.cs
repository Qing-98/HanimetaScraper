using HtmlAgilityPack;
using ScraperBackendService.Core.Util;
using System.Net;
using System.Text;
using System.Linq;

namespace ScraperBackendService.Core.Parsing;

/// <summary>
/// HtmlAgilityPack extension utilities
/// </summary>
public static class HtmlEx
{
    public static HtmlNode? SelectSingle(HtmlDocument doc, string xpath)
        => ScrapingUtils.SelectSingle(doc, xpath);

    public static IEnumerable<HtmlNode> SelectNodes(HtmlDocument doc, string xpath)
        => ScrapingUtils.SelectNodes(doc, xpath) ?? Enumerable.Empty<HtmlNode>();

    public static string SelectText(HtmlDocument doc, string xpath)
        => ScrapingUtils.SelectText(doc, xpath);

    public static string GetAttr(HtmlNode? node, string attributeName)
        => ScrapingUtils.GetAttr(node, attributeName);

    /// <summary>
    /// Extract text from table cell, prefer link text
    /// </summary>
    public static string ExtractOutlineCell(HtmlDocument doc, string xpath, Func<string, string>? cleaner = null)
    {
        var result = ScrapingUtils.ExtractOutlineCell(doc, xpath);
        return cleaner?.Invoke(result) ?? result;
    }

    /// <summary>
    /// Extract text from table cell, prefer any link text
    /// </summary>
    public static string ExtractOutlineCellPreferA(HtmlDocument doc, string xpath, Func<string, string>? cleaner = null)
    {
        var result = ScrapingUtils.ExtractOutlineCellPreferA(doc, xpath);
        return cleaner?.Invoke(result) ?? result;
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
