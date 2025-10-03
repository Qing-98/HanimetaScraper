using System.Text.RegularExpressions;

namespace ScraperBackendService.Core.Normalize
{
    /// <summary>
    /// Utility class for cleaning titles before returning to frontend.
    /// Removes subtitle annotations and other bracketed content that may interfere with title display.
    /// </summary>
    public static class TitleCleaner
    {
        /// <summary>
        /// Pattern to match various subtitle annotations in brackets.
        /// </summary>
        private static readonly Regex SubtitleAnnotationRegex = new Regex(
            @"\[中文字幕\]|\[繁体字幕\]|\[简体字幕\]|\[英文字幕\]|\[日文字幕\]|\[Korean字幕\]|\[한국어자막\]|" +
            @"\[중국어자막\]|\[中文\]|\[繁体\]|\[简体\]|\[英文\]|\[日文\]|\[Korean\]|\[한국어\]|\[중국어\]|" +
            @"\(中文字幕\)|\(繁体字幕\)|\(简体字幕\)|\(英文字幕\)|\(日文字幕\)|\(Korean字幕\)|\(한국어자막\)|" +
            @"\(중국어자막\)|\(中文\)|\(繁体\)|\(简体\)|\(英文\)|\(日文\)|\(Korean\)|\(한국어\)|\(중국어\)|" +
            @"【中文字幕】|【繁体字幕】|【简体字幕】|【英文字幕】|【日文字幕】|【Korean字幕】|【한국어자막】|" +
            @"【중국어자막】|【中文】|【繁体】|【简体】|【英文】|【日文】|【Korean】|【한국어】|【중국어】|" +
            @"＜中文字幕＞|＜繁体字幕＞|＜简体字幕＞|＜英文字幕＞|＜日文字幕＞|＜Korean字幕＞|＜한국어자막＞|" +
            @"＜중국어자막＞|＜中文＞|＜繁体＞|＜简体＞|＜英文＞|＜日文＞|＜Korean＞|＜한국어＞|＜중국어＞",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Additional patterns for common subtitle and language annotations.
        /// </summary>
        private static readonly Regex AdditionalAnnotationRegex = new Regex(
            @"\[字幕\]|\[SUB\]|\[DUB\]|\[RAW\]|\[无修正\]|\[有修正\]|\[修正版\]|\[无码\]|\[有码\]|" +
            @"\(字幕\)|\(SUB\)|\(DUB\)|\(RAW\)|\(无修正\)|\(有修正\)|\(修正版\)|\(无码\)|\(有码\)|" +
            @"【字幕】|【SUB】|【DUB】|【RAW】|【无修正】|【有修正】|【修正版】|【无码】|【有码】|" +
            @"＜字幕＞|＜SUB＞|＜DUB＞|＜RAW＞|＜无修正＞|＜有修正＞|＜修正版＞|＜无码＞|＜有码＞",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Cleans title by removing subtitle annotations and other unwanted bracketed content.
        /// </summary>
        /// <param name="title">The original title to clean.</param>
        /// <returns>Cleaned title with subtitle annotations removed.</returns>
        /// <example>
        /// var cleaned = TitleCleaner.CleanTitle("Example Title [中文字幕]");
        /// // Returns: "Example Title"
        /// 
        /// var cleaned2 = TitleCleaner.CleanTitle("[繁体字幕] Another Title (中文字幕)");
        /// // Returns: "Another Title"
        /// </example>
        public static string CleanTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var cleaned = title.Trim();

            // Remove subtitle annotations
            cleaned = SubtitleAnnotationRegex.Replace(cleaned, string.Empty);

            // Remove additional annotations
            cleaned = AdditionalAnnotationRegex.Replace(cleaned, string.Empty);

            // Clean up extra whitespace and normalize
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            return cleaned;
        }
    }
}