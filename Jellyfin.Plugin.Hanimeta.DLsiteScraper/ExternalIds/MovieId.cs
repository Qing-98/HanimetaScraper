using Jellyfin.Plugin.Hanimeta.Common.ExternalIds;

namespace Jellyfin.Plugin.Hanimeta.DLsiteScraper.ExternalIds
{
    /// <summary>
    /// External ID provider for DLsite movies.
    /// </summary>
    public class MovieId : BaseMovieId
    {
        /// <inheritdoc />
        public override string ProviderName => "DLsite";

        /// <inheritdoc />
        public override string Key => "DLsite";
    }
}
