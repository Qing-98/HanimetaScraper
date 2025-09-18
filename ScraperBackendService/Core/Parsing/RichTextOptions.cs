using System.Text.RegularExpressions;

namespace ScraperBackendService.Core.Parsing;

/// <summary>
/// 提取富文本时的高级选项；默认值尽量保持“安全&通用”。
/// </summary>
public sealed class RichTextOptions
{
    /// <summary>是否保留换行；为 false 时会把换行折叠为空格。</summary>
    public bool KeepNewLines { get; init; } = true;

    /// <summary>是否保留完全空行（比如 p &lt;br&gt; 产生的空行）。</summary>
    public bool PreserveEmptyLines { get; init; } = false;

    /// <summary>是否渲染列表（ul/ol 的 li 前加项目符号）。</summary>
    public bool RenderLists { get; init; } = true;

    /// <summary>列表项目的前缀符号。</summary>
    public string Bullet { get; init; } = "• ";

    /// <summary>最大段落数（null 表示不限制）。</summary>
    public int? MaxParagraphs { get; init; }

    /// <summary>最大字符数（null 表示不限制）。超过后会在末尾追加省略号 "…"。</summary>
    public int? MaxChars { get; init; }

    /// <summary>包含这些关键字的段落会被过滤（大小写不敏感）。</summary>
    public List<string> ExcludeContains { get; } = new();

    /// <summary>匹配这些正则的段落会被过滤。</summary>
    public List<Regex> ExcludeRegex { get; } = new();

    /// <summary>在最终拼接后，允许做一次自定义后处理。</summary>
    public Func<string, string>? PostProcess { get; init; }

    public static RichTextOptions Default { get; } = new();
}
