namespace Jellyfin.Plugin.Hanimeta.Models
{
    /// <summary>
    /// Base model for search results from content providers.
    /// </summary>
    public class BaseSearchResult
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
        /// Gets or sets the year.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the primary image URL.
        /// </summary>
        public string? Primary { get; set; }
    }
}
