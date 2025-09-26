namespace Jellyfin.Plugin.DLsiteScraper.Models;

/// <summary>
/// Represents a DLsite search result.
/// </summary>
public class DLsiteSearchResult
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
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the production year.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the primary image URL.
    /// </summary>
    public string? Primary { get; set; }
}
