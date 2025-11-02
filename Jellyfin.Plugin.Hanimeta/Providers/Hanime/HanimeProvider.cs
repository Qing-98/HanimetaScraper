using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Hanimeta.Client;
using Jellyfin.Plugin.Hanimeta.Configuration;
using Jellyfin.Plugin.Hanimeta.Extensions;
using Jellyfin.Plugin.Hanimeta.ExternalIds;
using Jellyfin.Plugin.Hanimeta.ExternalUrls;
using Jellyfin.Plugin.Hanimeta.Helpers;
using Jellyfin.Plugin.Hanimeta.Models;
using Jellyfin.Plugin.Hanimeta.Providers.Common;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.Providers.Hanime;

#region Models
/// <summary>
/// Represents Hanime content metadata.
/// This replaces the separate HanimeMetadata.cs file.
/// </summary>
public class HanimeMetadata : BaseMetadata
{
    /// <summary>
    /// Gets or sets the people information (voice actors, etc.).
    /// </summary>
    public HanimePerson[]? People { get; set; }
}

/// <summary>
/// Represents a person involved in Hanime content.
/// This replaces the separate HanimePerson.cs file.
/// </summary>
public class HanimePerson : BasePerson
{
}
#endregion

#region External ID
/// <summary>
/// External ID provider for Hanime content.
/// This replaces the separate HanimeMovieExternalId.cs file.
/// </summary>
public class HanimeMovieExternalId : BaseMovieId
{
    /// <inheritdoc />
    public override string ProviderName => "Hanime";

    /// <inheritdoc />
    public override string Key => "Hanime";
}
#endregion

#region Metadata Mapper
/// <summary>
/// Hanime metadata mapper.
/// Maps Hanime metadata to Jellyfin entities.
/// This replaces the separate HanimeMetadataMapper.cs file.
/// </summary>
public class HanimeMetadataMapper : BaseMetadataMapper<HanimeMetadata, HanimePerson, BaseSearchResult>
{
    private static readonly HanimeMetadataMapper instance = new();

    /// <inheritdoc />
    protected override string ProviderIdKey => "Hanime";

    /// <inheritdoc />
    protected override HanimetaPluginConfiguration GetConfiguration()
    {
        return Plugin.PluginConfig;
    }

    /// <inheritdoc />
    protected override void StoreSourceUrls(string id, string[] sourceUrls)
    {
        if (!string.IsNullOrWhiteSpace(id) && sourceUrls != null && sourceUrls.Length > 0)
        {
            HanimeExternalUrlStore.Instance.SetUrls(id, sourceUrls);
        }
    }

    /// <summary>
    /// Maps Hanime metadata to a Jellyfin Movie entity.
    /// </summary>
    /// <param name="metadata">The Hanime metadata.</param>
    /// <param name="movie">The movie entity to populate.</param>
    /// <param name="originalName">The original name of the content.</param>
    public static new void MapToMovie(HanimeMetadata metadata, Movie movie, string? originalName = null)
    {
        ((BaseMetadataMapper<HanimeMetadata, HanimePerson, BaseSearchResult>)instance).MapToMovie(metadata, movie, originalName);
    }

    /// <summary>
    /// Maps a search result to a Jellyfin remote search result.
    /// </summary>
    /// <param name="searchResult">The search result to map.</param>
    /// <returns>The mapped remote search result.</returns>
    public static new RemoteSearchResult MapToSearchResult(BaseSearchResult searchResult)
    {
        return ((BaseMetadataMapper<HanimeMetadata, HanimePerson, BaseSearchResult>)instance).MapToSearchResult(searchResult);
    }

    /// <summary>
    /// Creates PersonInfo objects from Hanime metadata.
    /// </summary>
    /// <param name="metadata">The Hanime metadata.</param>
    /// <returns>Collection of PersonInfo objects.</returns>
    public static IEnumerable<PersonInfo> CreatePersonInfos(HanimeMetadata metadata)
    {
        return metadata?.People != null
            ? instance.CreatePersonInfos(metadata.People)
            : Enumerable.Empty<PersonInfo>();
    }
}
#endregion

#region Unified API Client
/// <summary>
/// Unified HTTP client for communicating with the Hanime scraper backend service.
/// This replaces the separate HanimeApiClient.cs file.
/// </summary>
public class HanimeUnifiedApiClient : BaseScraperApiClient<HanimeMetadata, BaseSearchResult>
{
    private readonly ILogger<HanimeUnifiedApiClient> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HanimeUnifiedApiClient"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="baseUrl">The base URL of the backend service.</param>
    /// <param name="apiToken">The optional API token for authentication.</param>
    public HanimeUnifiedApiClient(ILogger<HanimeUnifiedApiClient> logger, string baseUrl, string? apiToken = null)
        : base(logger, baseUrl, "hanime", apiToken)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    protected override HanimeMetadata ParseMetadata(JsonElement data, string id)
    {
        var metadata = new HanimeMetadata();
        ParseBaseMetadata(data, id, metadata);
        metadata.People = ParsePeople(data);
        return metadata;
    }

    /// <inheritdoc />
    protected override IEnumerable<BaseSearchResult> ParseSearchResults(JsonElement data)
    {
        var results = new List<BaseSearchResult>();

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var result = ParseSearchResult(item);
                if (result != null)
                {
                    results.Add(result);
                }
            }
        }
        else if (data.ValueKind == JsonValueKind.Object)
        {
            var result = ParseSearchResult(data);
            if (result != null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    private BaseSearchResult? ParseSearchResult(JsonElement item)
    {
        var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var result = new BaseSearchResult();
        ParseBaseSearchResult(item, result);

        if (string.IsNullOrWhiteSpace(result.Primary))
        {
            logger.LogWarningIfEnabled($"Hanime search result missing primary image: ID={id}, Title={title}");
        }
        else
        {
            logger.LogDebugIfEnabled($"Hanime search result with image: ID={id}, Title={title}, Primary={result.Primary}");
        }

        return result;
    }

    private HanimePerson[] ParsePeople(JsonElement data)
    {
        if (!data.TryGetProperty("people", out var peopleProp) || peopleProp.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<HanimePerson>();
        }

        var people = new List<HanimePerson>();
        foreach (var person in peopleProp.EnumerateArray())
        {
            var name = person.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                people.Add(new HanimePerson
                {
                    Name = name,
                    Type = person.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null,
                    Role = person.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null
                });
            }
        }

        return people.ToArray();
    }
}
#endregion

#region Unified Metadata Provider
/// <summary>
/// Unified Hanime metadata provider.
/// This replaces the separate HanimeMetadataProvider.cs file.
/// </summary>
public class HanimeUnifiedMetadataProvider : BaseMetadataProviderService<HanimeMetadata, HanimePerson, HanimeUnifiedApiClient, HanimeMetadataMapper>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HanimeUnifiedMetadataProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="apiClientFactory">The API client factory.</param>
    /// <param name="mapper">The metadata mapper.</param>
    public HanimeUnifiedMetadataProvider(
        ILogger<HanimeUnifiedMetadataProvider> logger,
        Func<HanimeUnifiedApiClient> apiClientFactory,
        HanimeMetadataMapper mapper)
        : base(logger, apiClientFactory, mapper)
    {
    }

    /// <inheritdoc />
    public override string Name => "Hanime";

    /// <inheritdoc />
    protected override string ProviderIdKey => "Hanime";

    /// <inheritdoc />
    protected override IEnumerable<HanimePerson> GetPeople(HanimeMetadata metadata)
    {
        return metadata.People ?? Array.Empty<HanimePerson>();
    }

    /// <inheritdoc />
    protected override bool TryParseProviderId(string? input, out string id)
    {
        return HanimeProviderHelpers.TryParseProviderId(input, out id);
    }
}
#endregion

#region Unified Image Provider
/// <summary>
/// Unified Hanime image provider.
/// This replaces the separate HanimeImageProvider.cs file.
/// </summary>
public class HanimeUnifiedImageProvider : BaseImageProviderService<HanimeMetadata, HanimeUnifiedApiClient>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HanimeUnifiedImageProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="apiClientFactory">The API client factory.</param>
    public HanimeUnifiedImageProvider(ILogger<HanimeUnifiedImageProvider> logger, Func<HanimeUnifiedApiClient> apiClientFactory)
        : base(logger, apiClientFactory)
    {
    }

    /// <inheritdoc />
    public override string Name => "Hanime";

    /// <inheritdoc />
    protected override string ProviderIdKey => "Hanime";

    /// <inheritdoc />
    protected override bool TryParseProviderId(string? input, out string id)
    {
        return HanimeProviderHelpers.TryParseProviderId(input, out id);
    }
}
#endregion

#region Unified External URL Provider
/// <summary>
/// Unified Hanime external URL provider.
/// This replaces the separate HanimeExternalUrlProvider.cs file.
/// </summary>
public class HanimeUnifiedExternalUrlProvider : BaseExternalUrlProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HanimeUnifiedExternalUrlProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public HanimeUnifiedExternalUrlProvider(ILogger<HanimeUnifiedExternalUrlProvider> logger)
        : base(HanimeExternalUrlStore.Instance, "Hanime")
    {
    }

    /// <inheritdoc />
    public override string Name => "Hanime";

    /// <inheritdoc />
    public override string UrlFormatString => "https://hanime1.me/watch?v={0}";
}
#endregion

#region Provider Helpers
/// <summary>
/// Shared helper methods for Hanime provider components.
/// Centralizes common logic to avoid code duplication.
/// </summary>
internal static class HanimeProviderHelpers
{
    /// <summary>
    /// Attempts to parse a Hanime provider ID from various input formats.
    /// </summary>
    /// <param name="input">The input string to parse</param>
    /// <param name="id">The parsed ID if successful</param>
    /// <returns>True if parsing was successful</returns>
    public static bool TryParseProviderId(string? input, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = input.Trim();
        
        // Numeric ID (at least 4 digits)
        var numericIdRegex = new Regex(@"^\d{4,}$", RegexOptions.Compiled);
        if (numericIdRegex.IsMatch(cleaned))
        {
            id = cleaned;
            return true;
        }

        // ID in URL
        var urlIdRegex = new Regex(@"[?&]v=(\d+)", RegexOptions.Compiled);
        var urlMatch = urlIdRegex.Match(cleaned);
        if (urlMatch.Success)
        {
            id = urlMatch.Groups[1].Value;
            return true;
        }

        return false;
    }
}
#endregion
