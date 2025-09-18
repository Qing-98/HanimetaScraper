using System.Text.Json;
using Microsoft.Playwright;

namespace ScraperBackendService.Core.Abstractions;

/// <summary>
/// 统一的网络访问抽象：
/// - Http 实现：GetHtmlAsync / GetJsonAsync；OpenPageAsync 返回 null；
/// - Playwright 实现：三者都可用（GetHtmlAsync 可由 page.ContentAsync() 实现）。
/// </summary>
public interface INetworkClient
{
    Task<string> GetHtmlAsync(string url, CancellationToken ct);

    Task<JsonDocument> GetJsonAsync(
        string url,
        IDictionary<string, string>? headers,
        CancellationToken ct);

    /// <summary>
    /// 仅 Playwright 实现会返回 IPage；Http 实现可返回 null。
    /// Provider 使用前应判断是否为 null。
    /// </summary>
    Task<IPage?> OpenPageAsync(string url, CancellationToken ct);
}
