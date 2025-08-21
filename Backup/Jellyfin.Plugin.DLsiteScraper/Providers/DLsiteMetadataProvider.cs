using System.Net.Http;
using System.Text.Json;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.DLsiteScraper.Providers;

public class DLsiteMetadataProvider : IRemoteMetadataProvider<Movie, MovieInfo>
{
    public string Name => "DLsite Metadata";

    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Movie> { Item = new Movie(), HasMetadata = false };

        var id = info.GetProviderId("DLsite");
        if (string.IsNullOrEmpty(id))
            return result;

        using var client = new HttpClient();
        var response = await client.GetStringAsync($"http://localhost:8585/api/dlsite/{id}", cancellationToken);

        var data = JsonDocument.Parse(response).RootElement;

        result.Item.Name = data.GetProperty("Title").GetString();
        result.Item.Overview = data.GetProperty("Description").GetString();
        result.Item.ProductionYear = data.GetProperty("Year").GetInt32();
        result.Item.CommunityRating = (float?)data.GetProperty("Rating").GetDouble();
        result.HasMetadata = true;

        return result;
    }

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
    {
        // 简化：直接返回空
        return Enumerable.Empty<RemoteSearchResult>();
    }
}
