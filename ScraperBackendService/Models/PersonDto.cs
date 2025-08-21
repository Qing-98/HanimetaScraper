namespace ScraperBackendService.Models;

public sealed class PersonDto
{
    /// <summary>人物名称，例如“山田太郎”</summary>
    public string Name { get; set; } = "";

    /// <summary>人物类型：Actor / Director / Writer / Producer…（建议使用 Jellyfin 常用字符串）</summary>
    public string Type { get; set; } = "";

    /// <summary>可选的细分角色标注
    public string? Role { get; set; }
}
