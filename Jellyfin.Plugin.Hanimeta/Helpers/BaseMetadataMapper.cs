using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Hanimeta.Configuration;
using Jellyfin.Plugin.Hanimeta.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Hanimeta.Helpers
{
    /// <summary>
    /// Base helper class for mapping metadata to Jellyfin entities.
    /// </summary>
    /// <typeparam name="TMetadata">The metadata type.</typeparam>
    /// <typeparam name="TPerson">The person type.</typeparam>
    /// <typeparam name="TSearchResult">The search result type.</typeparam>
    public abstract class BaseMetadataMapper<TMetadata, TPerson, TSearchResult>
        where TMetadata : BaseMetadata
        where TPerson : BasePerson
        where TSearchResult : BaseSearchResult
    {
        /// <summary>
        /// Gets the provider ID key.
        /// </summary>
        protected abstract string ProviderIdKey { get; }

        /// <summary>
        /// Maps metadata to a Jellyfin Movie entity.
        /// </summary>
        /// <param name="metadata">The metadata.</param>
        /// <param name="movie">The Jellyfin movie entity to populate.</param>
        /// <param name="originalName">The original name from MovieInfo.</param>
        public virtual void MapToMovie(TMetadata metadata, Movie movie, string? originalName = null)
        {
            if (metadata == null || movie == null)
            {
                return;
            }

            // Set external ID
            if (!string.IsNullOrWhiteSpace(metadata.Id))
            {
                movie.SetProviderId(this.ProviderIdKey, metadata.Id);
            }

            // Basic information
            movie.Name = !string.IsNullOrWhiteSpace(metadata.Title) ? metadata.Title : originalName ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(metadata.OriginalTitle))
            {
                movie.OriginalTitle = metadata.OriginalTitle;
            }

            if (!string.IsNullOrWhiteSpace(metadata.Description))
            {
                movie.Overview = metadata.Description;
            }

            if (metadata.Year.HasValue)
            {
                movie.ProductionYear = metadata.Year.Value;
            }

            if (metadata.Rating.HasValue)
            {
                movie.CommunityRating = metadata.Rating.Value * 2; // Convert to 10-point scale
            }

            if (metadata.ReleaseDate.HasValue)
            {
                movie.PremiereDate = metadata.ReleaseDate;
            }

            // Studios
            if (metadata.Studios != null && metadata.Studios.Length > 0)
            {
                movie.Studios = metadata.Studios;
            }

            // Handle backend-returned genres - collect them separately
            var backendGenres = new List<string>();
            if (metadata.Genres != null && metadata.Genres.Length > 0)
            {
                backendGenres.AddRange(metadata.Genres);
            }

            // Handle series - always goes to Tags regardless of configuration
            var seriesToTags = new List<string>();
            if (metadata.Series != null && metadata.Series.Length > 0)
            {
                seriesToTags.AddRange(metadata.Series);
            }

            // Handle content tags based on configuration
            var config = this.GetConfiguration();
            var contentTags = new List<string>();
            if (metadata.Tags != null && metadata.Tags.Length > 0)
            {
                contentTags.AddRange(metadata.Tags);
            }

            // Apply the mapping based on configuration
            switch (config.TagMappingMode)
            {
                case TagMappingMode.Tags:
                    // Backend genres go to Genres
                    if (backendGenres.Count > 0)
                    {
                        movie.Genres = backendGenres.ToArray();
                    }

                    // Series + Content tags go to Tags
                    var allTags = new List<string>();
                    allTags.AddRange(seriesToTags);  // Series always in tags
                    allTags.AddRange(contentTags);   // Content tags in tags
                    if (allTags.Count > 0)
                    {
                        movie.Tags = allTags.ToArray();
                    }

                    break;

                case TagMappingMode.Genres:
                    // Backend genres + Content tags go to Genres, Series still goes to Tags
                    var allGenres = new List<string>();
                    allGenres.AddRange(backendGenres); // Backend genres first
                    allGenres.AddRange(contentTags);   // Content tags to genres
                    if (allGenres.Count > 0)
                    {
                        movie.Genres = allGenres.Distinct().ToArray();
                    }

                    // Series always goes to Tags (independent of configuration)
                    if (seriesToTags.Count > 0)
                    {
                        movie.Tags = seriesToTags.ToArray();
                    }

                    break;
            }

            // Handle source URLs
            if (metadata.Id != null && metadata.SourceUrls != null && metadata.SourceUrls.Length > 0)
            {
                this.StoreSourceUrls(metadata.Id, metadata.SourceUrls);
            }
        }

        /// <summary>
        /// Maps search result to a Jellyfin RemoteSearchResult.
        /// </summary>
        /// <param name="searchResult">The search result.</param>
        /// <returns>The RemoteSearchResult.</returns>
        public virtual RemoteSearchResult MapToSearchResult(TSearchResult searchResult)
        {
            if (searchResult == null)
            {
                return new RemoteSearchResult();
            }

            var result = new RemoteSearchResult
            {
                // Prefer OriginalTitle if provided by backend
                Name = !string.IsNullOrWhiteSpace(searchResult.OriginalTitle) ?
                    searchResult.OriginalTitle : (searchResult.Title ?? string.Empty),
                Overview = searchResult.Description,
                ProductionYear = searchResult.Year,
                ImageUrl = searchResult.Primary,
            };

            if (!string.IsNullOrWhiteSpace(searchResult.Id))
            {
                result.ProviderIds[this.ProviderIdKey] = searchResult.Id;
            }

            return result;
        }

        /// <summary>
        /// Create PersonInfo objects from metadata people.
        /// </summary>
        /// <param name="people">The people array.</param>
        /// <returns>An enumerable of PersonInfo instances.</returns>
        public virtual IEnumerable<PersonInfo> CreatePersonInfos(IEnumerable<TPerson> people)
        {
            if (people == null)
            {
                return Enumerable.Empty<PersonInfo>();
            }

            return people
                .Where(p => !string.IsNullOrWhiteSpace(p?.Name))
                .Select(p => new PersonInfo
                {
                    Name = p.Name?.Trim() ?? string.Empty,
                    Role = string.IsNullOrWhiteSpace(p.Role) ? (p.Type ?? string.Empty) : p.Role,
                    Type = this.MapPersonKind(p.Type),
                });
        }

        /// <summary>
        /// Gets the plugin configuration for tag mapping mode.
        /// </summary>
        /// <returns>The plugin configuration instance.</returns>
        protected abstract HanimetaPluginConfiguration GetConfiguration();

        /// <summary>
        /// Store source URLs for a content ID.
        /// </summary>
        /// <param name="id">The content ID.</param>
        /// <param name="sourceUrls">The source URLs.</param>
        protected abstract void StoreSourceUrls(string id, string[] sourceUrls);

        /// <summary>
        /// Maps a person type string to PersonKind.
        /// </summary>
        /// <param name="type">The person type string.</param>
        /// <returns>The mapped PersonKind.</returns>
        protected virtual PersonKind MapPersonKind(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return default;
            }

            // Try to parse standardized type directly to PersonKind
            if (Enum.TryParse<PersonKind>(type.Trim(), true, out var kind))
            {
                return kind;
            }

            // Fallback mapping
            return type?.ToLowerInvariant() switch
            {
                "actor" => PersonKind.Actor,
                "director" => PersonKind.Director,
                "writer" => PersonKind.Writer,
                "producer" => PersonKind.Producer,
                "composer" => PersonKind.Composer,
                _ => default,
            };
        }
    }
}
