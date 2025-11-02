using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;

namespace Jellyfin.Plugin.Hanimeta.ExternalUrls
{
    /// <summary>
    /// Base class for providing external URLs for media items.
    /// </summary>
    public abstract class BaseExternalUrlProvider : IExternalUrlProvider
    {
        private readonly BaseExternalUrlStore urlStore;
        private readonly string providerKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseExternalUrlProvider"/> class.
        /// </summary>
        /// <param name="urlStore">The URL store.</param>
        /// <param name="providerKey">The provider key for IDs.</param>
        protected BaseExternalUrlProvider(BaseExternalUrlStore urlStore, string providerKey)
        {
            this.urlStore = urlStore;
            this.providerKey = providerKey;
        }

        /// <summary>
        /// Gets the name of the external service.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the URL format string.
        /// </summary>
        public virtual string UrlFormatString => "https://example.com/{0}";

        /// <summary>
        /// Gets the external URLs for the specified item.
        /// </summary>
        /// <param name="item">The item to get URLs for.</param>
        /// <returns>A collection of external URLs.</returns>
        public IEnumerable<string> GetExternalUrls(BaseItem item)
        {
            if (item?.ProviderIds == null)
            {
                return System.Array.Empty<string>();
            }

            if (item.ProviderIds.TryGetValue(this.providerKey, out var id) && !string.IsNullOrWhiteSpace(id))
            {
                var url = this.urlStore.GetUrl(id);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return new[] { url };
                }
                else
                {
                    return new[] { string.Format(this.UrlFormatString, id) };
                }
            }

            return System.Array.Empty<string>();
        }
    }
}
