using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.HanimeScraper.ExternalIds;

/// <summary>
/// External ID provider for Hanime movies.
/// </summary>
public class MovieId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => "Hanime";

    /// <inheritdoc />
    public string Key => "Hanime";

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

    /// <inheritdoc />
    public string? UrlFormatString => "https://hanime1.me/watch?v={0}";

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is Movie;
}
