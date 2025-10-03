using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Hanimeta.Common.Helpers
{
    /// <summary>
    /// Utility helpers for extracting and normalizing provider IDs.
    /// </summary>
    public static class IdHelper
    {
        private static readonly Regex DlsiteExact = new(@"^(RJ|VJ)\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DlsiteLoose = new(@"(RJ|VJ)\d{3,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HanimeNumber = new(@"^\d{4,}$", RegexOptions.Compiled);
        private static readonly Regex HanimeUrl = new(@"[?&]v=(\d+)", RegexOptions.Compiled);

        /// <summary>
        /// Tries to extract a DLsite product ID from the given input (accepts formats like "RJ123456" or urls containing RJ/VJ codes).
        /// </summary>
        /// <param name="input">Input string to parse.</param>
        /// <param name="id">The extracted ID if successful.</param>
        /// <returns>True if extraction succeeded; otherwise false.</returns>
        public static bool TryExtractDlsiteId(string? input, out string id)
        {
            id = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var s = input.Trim().ToUpperInvariant();

            if (DlsiteExact.IsMatch(s))
            {
                id = s;
                return true;
            }

            var m = DlsiteLoose.Match(s);
            if (m.Success)
            {
                id = m.Value.ToUpperInvariant();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Builds the canonical DLsite URL for the given product ID. If the ID starts with VJ, the pro domain will be used; RJ will use maniax.
        /// </summary>
        /// <param name="id">Product ID.</param>
        /// <param name="preferManiax">Whether to prefer maniax domain for ambiguous IDs.</param>
        /// <returns>Canonical product URL.</returns>
        public static string BuildDlsiteCanonicalUrl(string id, bool preferManiax = true)
        {
            id = id.Trim().ToUpperInvariant();

            var template = preferManiax
                ? "https://www.dlsite.com/maniax/work/=/product_id/{0}.html"
                : "https://www.dlsite.com/pro/work/=/product_id/{0}.html";

            if (id.StartsWith("VJ", StringComparison.OrdinalIgnoreCase))
            {
                template = "https://www.dlsite.com/pro/work/=/product_id/{0}.html";
            }
            else if (id.StartsWith("RJ", StringComparison.OrdinalIgnoreCase))
            {
                template = "https://www.dlsite.com/maniax/work/=/product_id/{0}.html";
            }

            return string.Format(CultureInfo.InvariantCulture, template, id);
        }

        /// <summary>
        /// Tries to extract a Hanime numeric ID from input (either plain number of 4+ digits or a URL with v= query parameter).
        /// </summary>
        /// <param name="input">Input string.</param>
        /// <param name="id">Extracted ID when successful.</param>
        /// <returns>True if extraction succeeded; otherwise false.</returns>
        public static bool TryExtractHanimeId(string? input, out string id)
        {
            id = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var s = input.Trim();

            if (HanimeNumber.IsMatch(s))
            {
                id = s;
                return true;
            }

            var m = HanimeUrl.Match(s);
            if (m.Success)
            {
                id = m.Groups[1].Value;
                return true;
            }

            return false;
        }
    }
}
