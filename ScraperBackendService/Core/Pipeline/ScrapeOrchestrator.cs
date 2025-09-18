using Microsoft.Extensions.Logging;
using ScraperBackendService.Core.Abstractions;
using ScraperBackendService.Core.Normalize;
using ScraperBackendService.Core.Util;
using ScraperBackendService.Models;

namespace ScraperBackendService.Core.Pipeline;

/// <summary>
/// 统一的抓取编排器：
/// - 根据 ScrapeRoute (Auto/ById/ByFilename) 决定走哪条路
/// - 负责关键词清洗 / 并发抓详情 / 保持结果顺序
/// - 上层（Jellyfin 插件或 API 控制器）只需要调用 FetchAsync
/// </summary>
public sealed class ScrapeOrchestrator
{
    private readonly IMediaProvider _provider;
    private readonly INetworkClient _net;
    private readonly ILogger _log;

    public ScrapeOrchestrator(IMediaProvider provider, INetworkClient net, ILogger log)
    {
        _provider = provider;
        _net = net;
        _log = log;
    }

    public async Task<List<HanimeMetadata>> FetchAsync(
        string input,
        ScrapeRoute route,
        int maxResults,
        CancellationToken ct)
    {
        switch (route)
        {
            case ScrapeRoute.ById:
                if (!_provider.TryParseId(input, out var id))
                    throw new ArgumentException($"按ID解析失败：{input}");
                return await FetchByIdAsync(id, ct);

            case ScrapeRoute.ByFilename:
                return await SearchAndFetchAsync(input, maxResults, ct);

            case ScrapeRoute.Auto:
            default:
                if (_provider.TryParseId(input, out var id2))
                    return await FetchByIdAsync(id2, ct);
                return await SearchAndFetchAsync(input, maxResults, ct);
        }
    }

    private async Task<List<HanimeMetadata>> FetchByIdAsync(string id, CancellationToken ct)
    {
        var url = _provider.BuildDetailUrlById(id);
        var meta = await _provider.FetchDetailAsync(url, ct);
        return (meta is null) ? new() : new() { meta };
    }

    private async Task<List<HanimeMetadata>> SearchAndFetchAsync(
        string filenameOrText, int maxResults, CancellationToken ct)
    {
        var kw = TextNormalizer.BuildQueryFromFilename(filenameOrText);
        if (string.IsNullOrWhiteSpace(kw))
            kw = filenameOrText?.Trim() ?? "";

        var hits = await _provider.SearchAsync(kw, maxResults, ct);

        var results = await OrderedAsync.ForEachAsync(hits.ToList(), degree: 4, async h =>
        {
            var m = await _provider.FetchDetailAsync(h.DetailUrl, ct);
            if (m == null) return null;

            // 用搜索结果补全
            if (!string.IsNullOrWhiteSpace(h.Title) && string.IsNullOrWhiteSpace(m.Title))
                m.Title = h.Title;
            if (!string.IsNullOrWhiteSpace(h.CoverUrl) && string.IsNullOrWhiteSpace(m.Primary))
                m.Primary = h.CoverUrl;
            if (!m.SourceUrls.Contains(h.DetailUrl))
                m.SourceUrls.Add(h.DetailUrl);

            return m;
        });

        return results;
    }
}
