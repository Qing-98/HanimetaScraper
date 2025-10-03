using Jellyfin.Plugin.Hanimeta.Common.ExternalIds;

namespace Jellyfin.Plugin.Hanimeta.HanimeScraper.ExternalIds
{
    /// <summary>
    /// External ID provider for Hanime movies.
    /// </summary>
    public class MovieId : BaseMovieId
    {
        /// <inheritdoc />
        public override string ProviderName => "Hanime";

        /// <inheritdoc />
        public override string Key => "Hanime";
    }
}
