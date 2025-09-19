using System.Text.RegularExpressions;

namespace ScraperBackendService.Core.Parsing;

/// <summary>
/// Advanced options for rich text extraction; defaults aim to be "safe & universal".
/// </summary>
public sealed class RichTextOptions
{
    /// <summary>Whether to preserve line breaks; when false, line breaks are collapsed to spaces.</summary>
    public bool KeepNewLines { get; init; } = true;

    /// <summary>Whether to preserve completely empty lines (e.g., empty lines produced by p &lt;br&gt;).</summary>
    public bool PreserveEmptyLines { get; init; } = false;

    /// <summary>Whether to render lists (add bullet points before ul/ol li items).</summary>
    public bool RenderLists { get; init; } = true;

    /// <summary>Prefix symbol for list items.</summary>
    public string Bullet { get; init; } = "• ";

    /// <summary>Maximum number of paragraphs (null means no limit).</summary>
    public int? MaxParagraphs { get; init; }

    /// <summary>Maximum number of characters (null means no limit). When exceeded, ellipsis "…" will be appended.</summary>
    public int? MaxChars { get; init; }

    /// <summary>Paragraphs containing these keywords will be filtered out (case insensitive).</summary>
    public List<string> ExcludeContains { get; } = new();

    /// <summary>Paragraphs matching these regex patterns will be filtered out.</summary>
    public List<Regex> ExcludeRegex { get; } = new();

    /// <summary>Allows custom post-processing after final concatenation.</summary>
    public Func<string, string>? PostProcess { get; init; }

    public static RichTextOptions Default { get; } = new();
}
