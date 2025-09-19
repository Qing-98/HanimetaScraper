namespace ScraperBackendService.Models;

/// <summary>
/// Person data transfer object for representing personnel information.
/// Used to store cast and crew details in a provider-agnostic format.
/// </summary>
/// <example>
/// Usage examples:
/// 
/// // Voice actor with original Japanese role
/// var actor = new PersonDto 
/// { 
///     Name = "田中花音", 
///     Type = "Actor", 
///     Role = "声優" 
/// };
/// 
/// // Director with English role
/// var director = new PersonDto 
/// { 
///     Name = "John Smith", 
///     Type = "Director", 
///     Role = "Director" 
/// };
/// 
/// // Writer with specific role detail
/// var writer = new PersonDto 
/// { 
///     Name = "Jane Doe", 
///     Type = "Writer", 
///     Role = "Original Story" 
/// };
/// 
/// // Producer without specific role detail
/// var producer = new PersonDto 
/// { 
///     Name = "Producer Name", 
///     Type = "Producer" 
/// };
/// </example>
public sealed class PersonDto
{
    /// <summary>
    /// Person's name.
    /// </summary>
    /// <example>
    /// "田中花音", "John Smith", "山田太郎"
    /// </example>
    public string Name { get; set; } = "";

    /// <summary>
    /// Person's role type using standardized English terms.
    /// Recommended values align with Jellyfin's personnel categories.
    /// </summary>
    /// <example>
    /// Common types:
    /// - "Actor" (voice actors, performers)
    /// - "Director" (directors, supervisors)
    /// - "Writer" (writers, scenario authors)
    /// - "Producer" (producers, planners)
    /// - "Composer" (music composers)
    /// - "Artist" (character designers, artists)
    /// </example>
    public string Type { get; set; } = "";

    /// <summary>
    /// Optional detailed role description or original role name.
    /// Preserves source language role information for reference.
    /// </summary>
    /// <example>
    /// // Japanese original roles
    /// Role = "声優";        // Voice actor
    /// Role = "監督";        // Director  
    /// Role = "シナリオ";     // Scenario writer
    /// Role = "音響監督";     // Sound director
    /// 
    /// // English role specifications
    /// Role = "Main Character";
    /// Role = "Supporting Character";
    /// Role = "Original Story";
    /// Role = "Character Design";
    /// </example>
    public string? Role { get; set; }
}
