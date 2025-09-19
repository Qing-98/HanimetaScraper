// ScraperBackendService/Models/MovieMeta.cs
namespace ScraperBackendService.Models;

/// <summary>
/// Comprehensive metadata model for adult content from various providers.
/// Stores all extracted information in a provider-agnostic format suitable for Jellyfin integration.
/// </summary>
/// <example>
/// Usage examples:
/// 
/// // Create new metadata instance
/// var metadata = new HanimeMetadata
/// {
///     ID = "12345",
///     Title = "Example Content",
///     Description = "Content description...",
///     Rating = 4.5,
///     ReleaseDate = DateTimeOffset.Parse("2024-01-15"),
///     Year = 2024
/// };
/// 
/// // Add studios and genres
/// metadata.Studios.Add("Studio Example");
/// metadata.Genres.AddRange(new[] { "Romance", "Comedy", "Drama" });
/// 
/// // Add personnel
/// metadata.People.Add(new PersonDto 
/// { 
///     Name = "Voice Actor Name", 
///     Type = "Actor", 
///     Role = "声優" 
/// });
/// 
/// // Set images
/// metadata.Primary = "https://example.com/cover.jpg";
/// metadata.Backdrop = "https://example.com/backdrop.jpg";
/// metadata.Thumbnails.AddRange(new[] { "thumb1.jpg", "thumb2.jpg" });
/// 
/// // Add source URL
/// metadata.SourceUrls.Add("https://source-site.com/content/12345");
/// </example>
public sealed class HanimeMetadata
{
    /// <summary>
    /// Content title in the target language.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Original title in the source language (often Japanese).
    /// </summary>
    public string? OriginalTitle { get; set; }

    /// <summary>
    /// Provider-specific content identifier.
    /// </summary>
    /// <example>
    /// "12345" (Hanime), "RJ123456" (DLsite), "ABC123" (other providers)
    /// </example>
    public string? ID { get; set; }

    /// <summary>
    /// Content description or synopsis.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Content rating on a 0-5 scale.
    /// Frontend can multiply by 2 to get 0-10 scale if needed.
    /// </summary>
    /// <example>
    /// 0.0 = No rating/Unknown
    /// 2.5 = Average (50%)
    /// 4.5 = Excellent (90%)
    /// 5.0 = Perfect (100%)
    /// </example>
    public double? Rating { get; set; }

    /// <summary>
    /// Content release date with timezone information.
    /// </summary>
    public DateTimeOffset? ReleaseDate { get; set; }

    /// <summary>
    /// Release year extracted from ReleaseDate for convenience.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Production studios or circles involved in content creation.
    /// </summary>
    /// <example>
    /// Studios.Add("Studio Name");
    /// Studios.AddRange(new[] { "Primary Studio", "Co-Production Studio" });
    /// </example>
    public List<string> Studios { get; set; } = new();

    /// <summary>
    /// Content genres and tags for categorization.
    /// </summary>
    /// <example>
    /// Genres.AddRange(new[] { "Romance", "Comedy", "School", "Slice of Life" });
    /// </example>
    public List<string> Genres { get; set; } = new();

    /// <summary>
    /// Series or franchise information if content is part of a larger collection.
    /// </summary>
    /// <example>
    /// Series.Add("Example Series");
    /// Series.Add("Example Series Season 2");
    /// </example>
    public List<string> Series { get; set; } = new();

    /// <summary>
    /// Personnel information including voice actors, directors, writers, etc.
    /// Backend-independent format that doesn't rely on Jellyfin-specific structures.
    /// </summary>
    /// <example>
    /// People.Add(new PersonDto { Name = "Actor Name", Type = "Actor", Role = "Main Character" });
    /// People.Add(new PersonDto { Name = "Director Name", Type = "Director", Role = "監督" });
    /// People.Add(new PersonDto { Name = "Writer Name", Type = "Writer", Role = "Original Story" });
    /// </example>
    public List<PersonDto> People { get; set; } = new();

    /// <summary>
    /// Primary cover image URL.
    /// Main poster or cover image for the content.
    /// </summary>
    /// <example>
    /// Primary = "https://example.com/images/covers/12345_cover.jpg";
    /// </example>
    public string? Primary { get; set; }

    /// <summary>
    /// Backdrop/background image URL (optional).
    /// Wide background image suitable for player backgrounds.
    /// </summary>
    /// <example>
    /// Backdrop = "https://example.com/images/backdrops/12345_backdrop.jpg";
    /// </example>
    public string? Backdrop { get; set; }

    /// <summary>
    /// Collection of thumbnail image URLs.
    /// Additional promotional or preview images.
    /// </summary>
    /// <example>
    /// Thumbnails.AddRange(new[] 
    /// {
    ///     "https://example.com/thumbs/12345_01.jpg",
    ///     "https://example.com/thumbs/12345_02.jpg",
    ///     "https://example.com/thumbs/12345_03.jpg"
    /// });
    /// </example>
    public List<string> Thumbnails { get; set; } = new();

    /// <summary>
    /// Source URLs where this content information was obtained.
    /// Useful for reference and debugging purposes.
    /// </summary>
    /// <example>
    /// SourceUrls.Add("https://hanime1.me/watch?v=12345");
    /// SourceUrls.Add("https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html");
    /// </example>
    public List<string> SourceUrls { get; set; } = new();
}
