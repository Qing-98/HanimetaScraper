using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.DLsiteScraper.Providers;

/// <summary>
/// DLsite 元数据提供器：与后端服务交互，不做页面解析。
/// </summary>
public class HanimeMetadataProvider
    : IRemoteMetadataProvider<Movie, MovieInfo>, IRemoteSearchProvider<MovieInfo>
{
    public string Name => "DLsite Metadata";

    public async Task<MetadataResult<Movie>> GetMetadata(
        MovieInfo info,
        CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Movie> { Item = new Movie(), HasMetadata = false };

        var id = info.GetProviderId("DLsite");
        if (string.IsNullOrWhiteSpace(id))
            return result;

        using var client = new HttpClient();
        var json = await client.GetStringAsync($"http://localhost:8585/api/dlsite/{id}", cancellationToken);
        var root = JsonDocument.Parse(json).RootElement;

        result.Item.Name = root.TryGetProperty("Title", out var t) ? (t.GetString() ?? info.Name ?? "") : (info.Name ?? "");
        if (root.TryGetProperty("Description", out var d)) result.Item.Overview = d.GetString();
        if (root.TryGetProperty("Year", out var y) && y.TryGetInt32(out var year)) result.Item.ProductionYear = year;
        if (root.TryGetProperty("Rating", out var r) && r.ValueKind == JsonValueKind.Number)
            result.Item.CommunityRating = (float)r.GetDouble();

        if (root.TryGetProperty("Tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tags.EnumerateArray())
            {
                var v = tag.GetString();
                if (!string.IsNullOrWhiteSpace(v)) result.Item.AddTag(v!);
            }
        }

        result.HasMetadata = true;
        return result;
    }

    // 这里将来可以改为调用你后端的搜索接口；先返回空列表占位。
    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        MovieInfo searchInfo,
        CancellationToken cancellationToken)
        => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());

    // 让 Jellyfin 通过我们获取远程图片（简单透传即可）
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var http = new HttpClient();
        return http.GetAsync(url, cancellationToken);
    }
}
