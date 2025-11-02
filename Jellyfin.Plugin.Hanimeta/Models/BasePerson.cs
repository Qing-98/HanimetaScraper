namespace Jellyfin.Plugin.Hanimeta.Models
{
    /// <summary>
    /// Base model for a person associated with content.
    /// </summary>
    public abstract class BasePerson
    {
        /// <summary>
        /// Gets or sets the name of the person.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the type of involvement (Actor, Director, etc.).
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Gets or sets the specific role description.
        /// </summary>
        public string? Role { get; set; }
    }
}
