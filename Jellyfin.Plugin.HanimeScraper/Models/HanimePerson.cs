namespace Jellyfin.Plugin.HanimeScraper.Models;

/// <summary>
/// Represents a person involved in Hanime content.
/// </summary>
public class HanimePerson
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
