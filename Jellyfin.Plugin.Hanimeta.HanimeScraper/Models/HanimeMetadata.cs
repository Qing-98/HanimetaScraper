using System;
using Jellyfin.Plugin.Hanimeta.Common.Models;

namespace Jellyfin.Plugin.Hanimeta.HanimeScraper.Models;

/// <summary>
/// Represents Hanime content metadata.
/// </summary>
public class HanimeMetadata : BaseMetadata
{
    /// <summary>
    /// Gets or sets the people information.
    /// </summary>
    public HanimePerson[]? People { get; set; } = Array.Empty<HanimePerson>();
}
