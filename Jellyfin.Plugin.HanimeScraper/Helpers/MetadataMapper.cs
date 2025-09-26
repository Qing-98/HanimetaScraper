using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.HanimeScraper.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.HanimeScraper.Helpers;

/// <summary>
/// Helper class for mapping Hanime metadata to Jellyfin entities.
/// </summary>
public static class MetadataMapper
{
    /// <summary>
    /// Maps Hanime metadata to a Jellyfin Movie entity.
    /// </summary>
    /// <param name="metadata">The Hanime metadata.</param>
    /// <param name="movie">The Jellyfin movie entity to populate.</param>
    /// <param name="originalName">The original name from MovieInfo.</param>
    public static void MapToMovie(HanimeMetadata metadata, Movie movie, string? originalName = null)
    {
        // Set external ID
        movie.SetProviderId("Hanime", metadata.Id);

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
            movie.PremiereDate = metadata.ReleaseDate.Value;
        }

        // Arrays
        movie.Genres = metadata.Genres;
        movie.Studios = metadata.Studios;
        movie.Tags = metadata.Series; // Map series to tags
    }

    /// <summary>
    /// Maps Hanime search result to Jellyfin search result.
    /// </summary>
    /// <param name="searchResult">The Hanime search result.</param>
    /// <returns>RemoteSearchResult for Jellyfin.</returns>
    public static RemoteSearchResult MapToSearchResult(HanimeSearchResult searchResult)
    {
        var result = new RemoteSearchResult
        {
            Name = searchResult.Title ?? string.Empty,
            Overview = searchResult.Description,
            ProductionYear = searchResult.Year,
            ImageUrl = searchResult.Primary,
            ProviderIds = { ["Hanime"] = searchResult.Id }
        };

        // Debug logging for search result mapping
        System.Diagnostics.Debug.WriteLine($"[Hanime] MapToSearchResult: ID={searchResult.Id}, Title={searchResult.Title}, ImageUrl={result.ImageUrl}");

        return result;
    }
}
