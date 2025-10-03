using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Hanimeta.Common.Helpers
{
    /// <summary>
    /// Utility class for cleaning filenames to improve search quality.
    /// </summary>
    public static class FilenameCleaner
    {
        private static readonly Regex CommonPrefixSuffixRegex = new Regex(@"^\[(.*?)\]|[\[\(]?[0-9]{2,8}[\]\)]?|[\.\-_ ](720p|1080p|2160p|4k|8k|x264|x265|HEVC|aac|ac3|BluRay|WEB-DL|WebRip|WEBRip|BDRip|DVDRip|HDTV)|[\.\\_\-\(\)\[\]]", RegexOptions.IgnoreCase);

        /// <summary>
        /// Cleans a filename to improve search quality (legacy method for compatibility).
        /// </summary>
        /// <param name="filename">The filename to clean.</param>
        /// <returns>Cleaned filename.</returns>
        public static string CleanFileName(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return string.Empty;
            }

            // Remove file extension
            filename = RemoveFileExtension(filename);

            // Replace dots and underscores with spaces
            filename = filename.Replace('.', ' ').Replace('_', ' ');

            // Remove common prefixes, suffixes, and technical info
            filename = CommonPrefixSuffixRegex.Replace(filename, " ");

            // Replace multiple spaces with single space and trim
            filename = Regex.Replace(filename, @"\s+", " ").Trim();

            return filename;
        }

        /// <summary>
        /// Advanced filename cleaning with comprehensive rules (recommended method).
        /// </summary>
        /// <param name="input">The filename to clean.</param>
        /// <returns>Cleaned filename.</returns>
        public static string Clean(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var s = input.Trim();

            // Remove common file extensions
            s = Regex.Replace(
                s,
                @"\.(mkv|mp4|avi|wmv|flv|mov|ts|mpg|mpeg|m4v|webm)$",
                string.Empty,
                RegexOptions.IgnoreCase);

            // Remove bracketed content of various types (handles nested brackets)
            var bracketPatterns = new[]
            {
                @"\[[^\[\]]*\]",
                @"\([^\(\)]*\)",
                @"\{[^\{\}]*\}",
                @"【[^【】]*】",
                @"（[^（）]*）",
                @"<[^<>]*>",
            };

            bool changed;
            do
            {
                changed = false;
                foreach (var pattern in bracketPatterns)
                {
                    var replaced = Regex.Replace(s, pattern, " ", RegexOptions.Singleline);
                    if (replaced != s)
                    {
                        s = replaced;
                        changed = true;
                    }
                }
            }
            while (changed);

            // Remove any leftover single bracket characters
            s = Regex.Replace(s, @"[\[\]\(\)\{\}【】（）<>]", " ");

            // Remove specific blacklist tokens/words (case-insensitive)
            var blacklist = new[] { "hanime1.me", "hanime", "h動漫", "裏番", "線上看", "線上觀看", "線上", "中文字幕", "dlsite.com", "dlsite" };
            foreach (var token in blacklist)
            {
                s = Regex.Replace(s, Regex.Escape(token), string.Empty, RegexOptions.IgnoreCase);
            }

            // Remove common quality/resolution labels
            s = Regex.Replace(
                s,
                @"\b(?:4k|8k|hd|uhd|[0-9]{3,4}\s*p|720p|1080p|2160p|x264|x265|h264|hevc|bluray|bdrip|webrip|web[- ]?dl|hdrip|dvdrip|brrip)\b",
                string.Empty,
                RegexOptions.IgnoreCase);

            // Replace weird symbols (symbols other than letters/numbers/whitespace/dot/hyphen/underscore) with space
            s = Regex.Replace(s, @"[^\p{L}\p{N}\s.\-_]", " ");

            // Replace underscores with space
            s = Regex.Replace(s, "_", " ");

            // Replace other Unicode dash characters (except ASCII hyphen '-') with space
            s = Regex.Replace(s, "[\\p{Pd}&&[^-]]+", " ");

            // Remove hyphens that are not directly between two letters
            s = Regex.Replace(s, @"(?<!\p{L})-(?!\p{L})", " ");
            s = Regex.Replace(s, @"(?<!\p{L})-(?=\p{L})", " ");
            s = Regex.Replace(s, @"(?<=\p{L})-(?!\p{L})", " ");

            // Collapse multiple spaces
            s = Regex.Replace(s, @"\s+", " ").Trim();

            return s;
        }

        /// <summary>
        /// Removes file extension from a filename.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>Filename without extension.</returns>
        public static string RemoveFileExtension(string filename)
        {
            int lastDotIndex = filename.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                return filename.Substring(0, lastDotIndex);
            }

            return filename;
        }

        /// <summary>
        /// Extracts potential ID from a filename or search term.
        /// </summary>
        /// <param name="text">Text to extract ID from.</param>
        /// <param name="pattern">Regex pattern for ID extraction.</param>
        /// <returns>Extracted ID if found; otherwise null.</returns>
        public static string? ExtractId(string text, string pattern)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            return null;
        }
    }
}
