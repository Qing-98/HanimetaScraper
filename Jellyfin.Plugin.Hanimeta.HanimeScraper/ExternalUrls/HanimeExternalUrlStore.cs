#pragma warning disable IDE1006 // Allow uppercase static readonly field names in this file

using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Hanimeta.Common.ExternalUrls;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.HanimeScraper.ExternalUrls
{
    /// <summary>
    /// Stores URLs for Hanime content.
    /// </summary>
    public class HanimeExternalUrlStore : BaseExternalUrlStore
    {
        private static readonly Lazy<HanimeExternalUrlStore> LazyInstance = new(() => new HanimeExternalUrlStore());

        private HanimeExternalUrlStore()
        {
            // Private constructor to enforce singleton pattern
        }

        /// <summary>
        /// Gets the singleton instance of the store.
        /// </summary>
        public static HanimeExternalUrlStore Instance => LazyInstance.Value;

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
                // Use the first URL as the primary URL for the ID
                AddOrUpdate(id, validUrls[0]);
            }
        }
    }
}

#pragma warning restore IDE1006
