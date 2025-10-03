using Microsoft.Extensions.Logging;
using ScraperBackendService.Core.Abstractions;
using ScraperBackendService.Core.Normalize;
using ScraperBackendService.Core.Util;
using ScraperBackendService.Models;

namespace ScraperBackendService.Core.Pipeline;

/// <summary>
/// Unified scrape orchestrator:
/// - Decides which path to take based on ScrapeRoute (Auto/ById/ByFilename)
/// - Responsible for keyword cleaning / concurrent detail fetching / maintaining result order
/// - Upper layer (Jellyfin plugin or API controller) only needs to call FetchAsync
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

    public async Task<List<Metadata>> FetchAsync(
        string input,
        ScrapeRoute route,
        int maxResults,
        CancellationToken ct)
    {
        switch (route)
        {
            case ScrapeRoute.ById:
                if (!_provider.TryParseId(input, out var id))
                    throw new ArgumentException($"ID parsing failed: {input}");
                return await FetchByIdAsync(id, ct);

            case ScrapeRoute.ByFilename:
                return await SearchAndFetchAsync(input, maxResults, ct);

            case ScrapeRoute.Auto:
            default:
                // Auto mode now always uses search instead of intelligent ID detection
                // This ensures that title search remains title search even if the input looks like an ID
                return await SearchAndFetchAsync(input, maxResults, ct);
        }
    }

    private async Task<List<Metadata>> FetchByIdAsync(string id, CancellationToken ct)
    {
        var url = _provider.BuildDetailUrlById(id);
        var meta = await _provider.FetchDetailAsync(url, ct);
        return (meta is null) ? new() : new() { meta };
    }

    private async Task<List<Metadata>> SearchAndFetchAsync(
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

            // Use search results to fill missing fields
            if (string.IsNullOrWhiteSpace(m.Title) && !string.IsNullOrWhiteSpace(h.Title))
                m.Title = h.Title;
            if (string.IsNullOrWhiteSpace(m.Primary) && !string.IsNullOrWhiteSpace(h.CoverUrl))
                m.Primary = h.CoverUrl;

            return m;
        });

        return results.Where(m => m != null).ToList()!;
    }
}
