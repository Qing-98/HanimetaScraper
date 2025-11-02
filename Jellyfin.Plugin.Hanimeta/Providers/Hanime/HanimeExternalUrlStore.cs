using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Hanimeta.ExternalUrls;

namespace Jellyfin.Plugin.Hanimeta.Providers.Hanime;

/// <summary>
/// Stores URLs for Hanime content.
/// </summary>
public class HanimeExternalUrlStore : BaseExternalUrlStore
{
    private static readonly Lazy<HanimeExternalUrlStore> lazyInstance = new(() => new HanimeExternalUrlStore());

    private HanimeExternalUrlStore()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the store.
    /// </summary>
    public static HanimeExternalUrlStore Instance => lazyInstance.Value;

    /// <summary>
    /// Sets URLs for a Hanime ID.
    /// </summary>
    /// <param name="id">The Hanime ID.</param>
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

        if (validUrls.Length > 0)
        {
            AddOrUpdate(id, validUrls[0]);
        }
    }
}
