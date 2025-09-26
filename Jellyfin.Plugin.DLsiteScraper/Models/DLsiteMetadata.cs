using System;

namespace Jellyfin.Plugin.DLsiteScraper.Models;

/// <summary>
/// Represents DLsite content metadata.
/// </summary>
public class DLsiteMetadata
{
    /// <summary>
    /// Gets or sets the DLsite ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the original title.
    /// </summary>
    public string? OriginalTitle { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the production year.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the rating.
    /// </summary>
    public float? Rating { get; set; }

    /// <summary>
    /// Gets or sets the release date.
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets the primary image URL.
    /// </summary>
    public string? Primary { get; set; }

    /// <summary>
    /// Gets or sets the genres.
    /// </summary>
    public string[] Genres { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the studios.
    /// </summary>
    public string[] Studios { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the series.
    /// </summary>
    public string[] Series { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the people information.
    /// </summary>
    public DLsitePerson[] People { get; set; } = Array.Empty<DLsitePerson>();
}
