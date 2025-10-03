using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Hanimeta.Common.ExternalIds
{
    /// <summary>
    /// Base external ID provider for movies.
    /// </summary>
    public abstract class BaseMovieId : IExternalId
    {
        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public abstract string ProviderName { get; }

        /// <summary>
        /// Gets the provider key.
        /// </summary>
        public abstract string Key { get; }

        /// <summary>
        /// Gets the URL format string.
        /// </summary>
        public virtual string? UrlFormatString => null;

        /// <summary>
        /// Gets the external ID media type.
        /// </summary>
        public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

        /// <summary>
        /// Gets a value indicating whether this provider supports the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>True if supported; otherwise false.</returns>
        public bool Supports(IHasProviderIds item) => item is Movie;
    }
}
