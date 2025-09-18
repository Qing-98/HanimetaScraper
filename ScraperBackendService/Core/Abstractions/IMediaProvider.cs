using ScraperBackendService.Models;
using System.Text.Json;

namespace ScraperBackendService.Core.Abstractions;

/// <summary>
/// 站点无关的“媒体提供方”抽象：
/// - 路由解析（ID/URL）
/// - 搜索页解析（返回详情页 URL + 可能的标题/封面）
/// - 详情页解析（返回完整元数据）
/// </summary>
public interface IMediaProvider
{
    string Name { get; }

    // 路由相关
    bool TryParseId(string input, out string id);
    string BuildDetailUrlById(string id);

    // 搜索：输入清洗后的关键词，返回若干“命中项”
    Task<IReadOnlyList<SearchHit>> SearchAsync(
        string keyword, int maxResults, CancellationToken ct);

    // 详情：输入详情页 URL，解析为完整元数据
    Task<HanimeMetadata?> FetchDetailAsync(
        string detailUrl, CancellationToken ct);
}

/// <summary>搜索命中条目（连接搜索层与详情层的轻量 DTO）。</summary>
public sealed record SearchHit(string DetailUrl, string? Title, string? CoverUrl);
