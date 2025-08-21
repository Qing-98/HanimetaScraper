// ScraperBackendService/Models/MovieMeta.cs
namespace ScraperBackendService.Models;

public sealed class HanimeMetadata
{
    public string? Title { get; set; }
    public string? OriginalTitle { get; set; }
    public string? ID { get; set; }
    public string? Description { get; set; }
    public double? Rating { get; set; }                 // 0–5（前端乘2→0–10）
    public DateTimeOffset? ReleaseDate { get; set; }
    public int? Year { get; set; }

    public List<string> Studios { get; set; } = new();
    public List<string> Genres { get; set; } = new();
    public List<string> Series { get; set; } = new();

    // 人物统一放这里（后端不依赖 Jellyfin）
    public List<PersonDto> People { get; set; } = new();

    // 图片字段与 Jellyfin ImageType 对齐的命名
    public string? Primary { get; set; }                // 主封面
    public string? Backdrop { get; set; }               // 背景图（可选）
    public List<string> Thumbnails { get; set; } = new(); // 缩略图列表

    public List<string> SourceUrls { get; set; } = new();
}
