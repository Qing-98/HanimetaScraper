namespace ScraperBackendService.Core.Abstractions;

/// <summary>
///（可选）把站点里的“导演/脚本/原画/声优”等表头映射为统一的 Type/Role。
/// DLsite 用得到；Hanime 暂时可不实现。
/// </summary>
public interface IRoleMapper
{
    (string Type, string? Role) Map(string rawHeaderText);
}
