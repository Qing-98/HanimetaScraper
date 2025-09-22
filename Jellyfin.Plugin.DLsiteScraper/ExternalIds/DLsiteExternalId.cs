using Jellyfin.Plugin.DLsiteScraper;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.DLsiteScraper.ExternalIds;

/// <summary>
/// External ID for DLsite.
/// </summary>
public class DLsiteExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => "DLsite";

    /// <inheritdoc />
    public string Key => "DLsite";

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

    /// <inheritdoc />
    public string? UrlFormatString
    {
        get
        {
            try
            {
                var backend = Plugin.PluginConfig?.BackendUrl?.TrimEnd('/') ?? "http://127.0.0.1:8585";
                // Route through backend redirect endpoint which will choose correct DLsite url
                return backend + "/r/dlsite/{0}";
            }
            catch
            {
                return "https://www.dlsite.com/maniax/work/=/product_id/{0}.html";
            }
        }
    }

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is Movie;
}
