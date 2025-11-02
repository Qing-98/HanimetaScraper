using System;

namespace Jellyfin.Plugin.Hanimeta.Models
{
    /// <summary>
    /// Base metadata model for content providers.
    /// </summary>
    public abstract class BaseMetadata
    {
        /// <summary>
        /// Gets or sets the unique identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the original title (in original language).
        /// </summary>
        public string? OriginalTitle { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the release year.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the rating (out of 5).
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
        /// Gets or sets the studios.
        /// </summary>
        public string[]? Studios { get; set; }

        /// <summary>
        /// Gets or sets the tags for categorization.
        /// Primary field for content classification.
        /// </summary>
        public string[]? Tags { get; set; }

        /// <summary>
        /// Gets or sets the genres (reserved for future use).
        /// Currently unused - Tags field should be used instead.
        /// </summary>
        public string[]? Genres { get; set; }

        /// <summary>
        /// Gets or sets the series.
        /// </summary>
        public string[]? Series { get; set; }

        /// <summary>
        /// Gets or sets the source URLs.
        /// </summary>
        public string[]? SourceUrls { get; set; }
    }
}
