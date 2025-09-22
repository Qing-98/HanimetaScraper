using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.HanimeScraper.ExternalIds;

/// <summary>
/// External ID for Hanime.
/// </summary>
public class HanimeExternalId : IExternalId
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public string ProviderName => "Hanime";

    /// <summary>
    /// Gets the key.
    /// </summary>
    public string Key => "Hanime";

    /// <summary>
    /// Gets the type.
    /// </summary>
    public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

    /// <summary>
    /// Gets the URL format string.
    /// </summary>
    // Use hanime1.me watch URL which matches backend and detail links
    public string? UrlFormatString => "https://hanime1.me/watch?v={0}";

    /// <summary>
    /// Determines whether this provider supports the given item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>True if supported.</returns>
    public bool Supports(IHasProviderIds item) => item is Movie;
}
