using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Linq;                  // ★ 重要：为空序列需要
using HtmlAgilityPack;

namespace ScraperBackendService.Core.Parsing;

public static class RichTextExtractor
{
    public static string ExtractFrom(HtmlNode? root, RichTextOptions? options = null)
    {
        if (root == null) return "";
        var opt = options ?? RichTextOptions.Default;

        var paras = new List<string>();

        // 1.1 多类型 item（DLsite 常见）
        foreach (var item in root.SelectNodes(".//div[contains(@class,'work_parts_multitype_item') and contains(@class,'type_text')]")
                                  ?? Enumerable.Empty<HtmlNode>())   // ★ 修复
        {
            var ps = item.SelectNodes(".//p");
            if (ps is { Count: > 0 })
            {
                foreach (var p in ps) AddPara(paras, NodeText(p, opt));
            }
            else
            {
                AddPara(paras, NodeText(item, opt));
            }
        }

        // 1.2 段落 p
        foreach (var p in root.SelectNodes(".//p") ?? Enumerable.Empty<HtmlNode>())  // ★ 修复
            AddPara(paras, NodeText(p, opt));

        // 1.3 列表 ul/ol -> li
        if (opt.RenderLists)
        {
            foreach (var li in root.SelectNodes(".//ul/li | .//ol/li")
                                 ?? Enumerable.Empty<HtmlNode>())    // ★ 修复
            {
                var t = NodeText(li, opt);
                if (!string.IsNullOrWhiteSpace(t))
                    paras.Add(opt.Bullet + t);
            }
        }

        // 1.4 兜底
        if (paras.Count == 0)
        {
            var t = NodeText(root, opt);
            if (!string.IsNullOrWhiteSpace(t)) paras.Add(t);
        }

        // 2) 过滤
        if (opt.ExcludeContains.Count > 0 || opt.ExcludeRegex.Count > 0)
        {
            paras = paras.Where(p =>
            {
                if (string.IsNullOrWhiteSpace(p)) return false;
                foreach (var kw in opt.ExcludeContains)
                    if (p.Contains(kw, StringComparison.OrdinalIgnoreCase)) return false;
                foreach (var re in opt.ExcludeRegex)
                    if (re.IsMatch(p)) return false;
                return true;
            }).ToList();
        }

        // 3) 段落上限
        if (opt.MaxParagraphs is int maxP && maxP > 0 && paras.Count > maxP)
            paras = paras.Take(maxP).ToList();

        // 4) 拼接
        var text = string.Join("\n\n", paras.Where(s => !string.IsNullOrWhiteSpace(s)));

        // 5) 空白规整
        text = NormalizeWhitespace(text, opt);

        // 6) 字数上限
        if (opt.MaxChars is int maxC && maxC > 0 && text.Length > maxC)
            text = text[..Math.Max(0, maxC - 1)] + "…";

        // 7) 自定义后处理
        if (opt.PostProcess is not null)
            text = opt.PostProcess(text);

        return text;
    }

    public static string ExtractFrom(HtmlDocument doc, string xpath, RichTextOptions? options = null)
        => ExtractFrom(doc.DocumentNode.SelectSingleNode(xpath), options);

    // === 内部工具 ===
    private static string NodeText(HtmlNode n, RichTextOptions opt)
    {
        var sb = new StringBuilder();
        void Walk(HtmlNode x)
        {
            if (x.Name.Equals("script", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Equals("style", StringComparison.OrdinalIgnoreCase)) return;

            if (x.NodeType == HtmlNodeType.Text)
            {
                sb.Append(x.InnerText);
                return;
            }

            if (opt.KeepNewLines && string.Equals(x.Name, "br", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append('\n');
                return;
            }

            foreach (var c in x.ChildNodes) Walk(c);
        }

        Walk(n);
        var raw = WebUtility.HtmlDecode(sb.ToString());
        return NormalizeInline(raw, opt);
    }

    private static string NormalizeInline(string s, RichTextOptions opt)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Replace('\u00A0', ' ');
        s = Regex.Replace(s, "[ \t\r\f\v]+", " ");

        if (opt.KeepNewLines)
        {
            var lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = lines[i].Trim();
            s = string.Join("\n", lines.Where(l => l.Length > 0 || opt.PreserveEmptyLines));
        }
        else
        {
            s = s.Replace('\n', ' ').Trim();
        }
        return s.Trim();
    }

    private static string NormalizeWhitespace(string s, RichTextOptions opt)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        if (opt.KeepNewLines) s = Regex.Replace(s, "\n{3,}", "\n\n");
        return s.Trim();
    }

    private static void AddPara(List<string> list, string? raw)
    {
        raw = (raw ?? "").Trim();
        if (!string.IsNullOrEmpty(raw)) list.Add(raw);
    }
}
