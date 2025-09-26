namespace Jellyfin.Plugin.HanimeScraper.Models;

/// <summary>
/// Represents a Hanime search result.
/// </summary>
public class HanimeSearchResult
{
    /// <summary>
    /// Gets or sets the Hanime ID.
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
