using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.DLsiteScraper.ExternalIds;

/// <summary>
/// External ID provider for DLsite movies.
/// </summary>
public class MovieId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => "DLsite";

    /// <inheritdoc />
    public string Key => "DLsite";

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

    /// <inheritdoc />
    public string? UrlFormatString => "https://www.dlsite.com/maniax/work/=/product_id/{0}.html";

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is Movie;
}
