#pragma warning disable IDE1006 // Naming Styles
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Hanimeta.Common.Configuration;
using Jellyfin.Plugin.Hanimeta.Common.Helpers;
using Jellyfin.Plugin.Hanimeta.Common.Models;
using Jellyfin.Plugin.Hanimeta.HanimeScraper.Configuration;
using Jellyfin.Plugin.Hanimeta.HanimeScraper.ExternalUrls;
using Jellyfin.Plugin.Hanimeta.HanimeScraper.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Hanimeta.HanimeScraper.Helpers
{
    /// <summary>
    /// Helper class for mapping Hanime metadata to Jellyfin entities.
    /// </summary>
    public class MetadataMapper : BaseMetadataMapper<HanimeMetadata, HanimePerson, BaseSearchResult>
    {
        private static readonly MetadataMapper Instance = new();

        /// <inheritdoc />
        protected override string ProviderIdKey => "Hanime";

        /// <inheritdoc />
        protected override HanimetaPluginConfiguration GetConfiguration()
        {
            return Plugin.Instance?.Configuration ?? new HanimetaPluginConfiguration();
        }

        /// <inheritdoc />
        protected override void StoreSourceUrls(string id, string[] sourceUrls)
        {
            if (!string.IsNullOrWhiteSpace(id) && sourceUrls != null && sourceUrls.Length > 0)
            {
                HanimeExternalUrlStore.Instance.SetUrls(id, sourceUrls);
            }
        }

        /// <summary>
        /// Maps Hanime metadata to a Jellyfin Movie entity.
        /// </summary>
        /// <param name="metadata">The Hanime metadata.</param>
        /// <param name="movie">The Jellyfin movie entity to populate.</param>
        /// <param name="originalName">The original name from MovieInfo.</param>
        public static new void MapToMovie(HanimeMetadata metadata, Movie movie, string? originalName = null)
        {
            // Call the base class method through the instance
            ((BaseMetadataMapper<HanimeMetadata, HanimePerson, BaseSearchResult>)Instance).MapToMovie(metadata, movie, originalName);
        }

        /// <summary>
        /// Maps Hanime search result to Jellyfin search result.
        /// </summary>
        /// <param name="searchResult">The Hanime search result.</param>
        /// <returns>RemoteSearchResult for Jellyfin.</returns>
        public static new RemoteSearchResult MapToSearchResult(BaseSearchResult searchResult)
        {
            // Call the base class method through the instance
            return ((BaseMetadataMapper<HanimeMetadata, HanimePerson, BaseSearchResult>)Instance).MapToSearchResult(searchResult);
        }

        /// <summary>
        /// Create PersonInfo objects from HanimeMetadata.People for adding to MetadataResult.
        /// </summary>
        /// <param name="metadata">The Hanime metadata instance containing people.</param>
        /// <returns>An enumerable of <see cref="PersonInfo"/> instances created from metadata.</returns>
        public static IEnumerable<PersonInfo> CreatePersonInfos(HanimeMetadata metadata)
        {
            return metadata?.People != null
                ? Instance.CreatePersonInfos(metadata.People)
                : Enumerable.Empty<PersonInfo>();
        }
    }
}
#pragma warning restore IDE1006
