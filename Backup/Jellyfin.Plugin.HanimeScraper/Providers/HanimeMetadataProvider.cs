using System.Net.Http;
using System.Text.Json;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.HanimeScraper.Providers;

public class HanimeMetadataProvider : IRemoteMetadataProvider<Movie, MovieInfo>
{
    public string Name => "Hanime Metadata";

    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Movie> { Item = new Movie(), HasMetadata = false };

        var id = info.GetProviderId("Hanime");
        if (string.IsNullOrEmpty(id))
            return result;

        using var client = new HttpClient();
        var response = await client.GetStringAsync($"http://localhost:8585/api/hanime/{id}", cancellationToken);

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
        return Enumerable.Empty<RemoteSearchResult>();
    }
}
