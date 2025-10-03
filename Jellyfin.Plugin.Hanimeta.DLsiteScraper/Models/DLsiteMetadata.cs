using System;
using Jellyfin.Plugin.Hanimeta.Common.Models;

namespace Jellyfin.Plugin.Hanimeta.DLsiteScraper.Models;

/// <summary>
/// Represents DLsite content metadata.
/// </summary>
public class DLsiteMetadata : BaseMetadata
{
    /// <summary>
    /// Gets or sets the people information.
    /// </summary>
    public DLsitePerson[] People { get; set; } = Array.Empty<DLsitePerson>();
}
