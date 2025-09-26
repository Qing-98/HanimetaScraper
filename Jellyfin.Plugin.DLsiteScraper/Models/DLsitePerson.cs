namespace Jellyfin.Plugin.DLsiteScraper.Models;

/// <summary>
/// Represents a person involved in DLsite content.
/// </summary>
public class DLsitePerson
{
    /// <summary>
    /// Gets or sets the person's name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the person's type (e.g., Director, Writer, Actor).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the person's role.
    /// </summary>
    public string? Role { get; set; }
}
