#pragma warning disable IDE1006 // Naming rule: allow lowercase field names in this file

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Hanimeta.Common.ExternalUrls;

namespace Jellyfin.Plugin.Hanimeta.DLsiteScraper.ExternalUrls
{
    /// <summary>
    /// Stores URLs for DLsite content.
    /// </summary>
    public class DLsiteExternalUrlStore : BaseExternalUrlStore
    {
        private static readonly Lazy<DLsiteExternalUrlStore> LazyInstance = new(() => new DLsiteExternalUrlStore());

        private DLsiteExternalUrlStore()
        {
            // Private constructor to enforce singleton pattern
        }

        /// <summary>
        /// Gets the singleton instance of the store.
        /// </summary>
        public static DLsiteExternalUrlStore Instance => LazyInstance.Value;

        /// <summary>
        /// Sets URLs for a DLsite ID.
        /// Chooses a canonical DLsite URL (maniax/pro) when possible based on RJ/VJ ID.
        /// </summary>
        /// <param name="id">The DLsite ID.</param>
        /// <param name="urls">The URLs to set.</param>
        public void SetUrls(string id, string[] urls)
        {
            if (string.IsNullOrWhiteSpace(id) || urls == null || urls.Length == 0)
            {
                return;
            }

            var validUrls = urls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (validUrls.Length == 0)
            {
                return;
            }

            // Try to derive a canonical DLsite URL from the provided source URLs
            string? canonical = null;
            foreach (var u in validUrls)
            {
                var parsed = TryParseDlsiteIdFromUrl(u);
                if (!string.IsNullOrWhiteSpace(parsed))
                {
                    canonical = BuildDlsiteDetailUrl(parsed);
                    break;
                }
            }

            // If no canonical from urls, try to normalize provided id and build canonical
            if (string.IsNullOrWhiteSpace(canonical) && TryParseDlsiteId(id, out var normalizedId))
            {
                canonical = BuildDlsiteDetailUrl(normalizedId);
            }

            // Fallback to the first valid URL if no canonical could be constructed
            var selected = !string.IsNullOrWhiteSpace(canonical) ? canonical : validUrls[0];

            AddOrUpdate(id, selected);
        }

        private static string? TryParseDlsiteIdFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var m = Regex.Match(url, "(RJ|VJ)\\d{3,}", RegexOptions.IgnoreCase);
            return m.Success ? m.Value.ToUpperInvariant() : null;
        }

        private static bool TryParseDlsiteId(string? input, out string id)
        {
            id = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var s = input.Trim().ToUpperInvariant();
            var m = Regex.Match(s, "^(RJ|VJ)\\d+$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                id = s;
                return true;
            }

            var m2 = Regex.Match(s, "(RJ|VJ)\\d{3,}", RegexOptions.IgnoreCase);
            if (m2.Success)
            {
                id = m2.Value.ToUpperInvariant();
                return true;
            }

            return false;
        }

        private static string BuildDlsiteDetailUrl(string id, bool preferManiax = true)
        {
            id = id.Trim().ToUpperInvariant();
            string template = preferManiax
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

            return string.Format(System.Globalization.CultureInfo.InvariantCulture, template, id);
        }
    }
}

#pragma warning restore IDE1006
