using System.Collections.Generic;

namespace Jellyfin.Plugin.Hanimeta.Common.ExternalUrls
{
    /// <summary>
    /// Base class for storing external URLs.
    /// </summary>
    public abstract class BaseExternalUrlStore
    {
        private readonly Dictionary<string, string> idToUrlMap = new Dictionary<string, string>();

        /// <summary>
        /// Gets the count of stored URLs.
        /// </summary>
        /// <returns>The count.</returns>
        public int Count => this.idToUrlMap.Count;

        /// <summary>
        /// Adds or updates a URL mapping for an ID.
        /// </summary>
        /// <param name="id">The ID.</param>
        /// <param name="url">The URL.</param>
        public void AddOrUpdate(string id, string url)
        {
            this.idToUrlMap[id] = url;
        }

        /// <summary>
        /// Gets a URL for an ID.
        /// </summary>
        /// <param name="id">The ID.</param>
        /// <returns>The URL if found; otherwise null.</returns>
        public string? GetUrl(string id)
        {
            this.idToUrlMap.TryGetValue(id, out var url);
            return url;
        }

        /// <summary>
        /// Gets whether a URL exists for an ID.
        /// </summary>
        /// <param name="id">The ID.</param>
        /// <returns>True if a URL exists; otherwise false.</returns>
        public bool HasUrl(string id)
        {
            return this.idToUrlMap.ContainsKey(id);
        }

        /// <summary>
        /// Clears all stored URLs.
        /// </summary>
        public void Clear()
        {
            this.idToUrlMap.Clear();
        }
    }
}
