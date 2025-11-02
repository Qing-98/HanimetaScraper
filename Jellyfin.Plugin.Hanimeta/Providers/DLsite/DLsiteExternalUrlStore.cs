using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Hanimeta.ExternalUrls;

namespace Jellyfin.Plugin.Hanimeta.Providers.DLsite;

/// <summary>
/// Stores URLs for DLsite content.
/// </summary>
public class DLsiteExternalUrlStore : BaseExternalUrlStore
{
    private static readonly Lazy<DLsiteExternalUrlStore> lazyInstance = new(() => new DLsiteExternalUrlStore());

    private DLsiteExternalUrlStore()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the store.
    /// </summary>
    public static DLsiteExternalUrlStore Instance => lazyInstance.Value;

    /// <summary>
    /// Sets URLs for a DLsite ID.
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

        // Try to derive canonical DLsite URL
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

        if (string.IsNullOrWhiteSpace(canonical) && TryParseDlsiteId(id, out var normalizedId))
        {
            canonical = BuildDlsiteDetailUrl(normalizedId);
        }

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

    private static string BuildDlsiteDetailUrl(string id)
    {
        id = id.Trim().ToUpperInvariant();

        // Always use correct site based on ID prefix
        if (id.StartsWith("VJ", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://www.dlsite.com/pro/work/=/product_id/{id}.html";
        }
        else if (id.StartsWith("RJ", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://www.dlsite.com/maniax/work/=/product_id/{id}.html";
        }

        // Default to maniax for unknown prefixes
        return $"https://www.dlsite.com/maniax/work/=/product_id/{id}.html";
    }
}
