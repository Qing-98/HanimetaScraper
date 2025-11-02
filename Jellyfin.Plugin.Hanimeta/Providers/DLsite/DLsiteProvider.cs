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

namespace Jellyfin.Plugin.Hanimeta.Providers.DLsite;

#region Models
/// <summary>
/// Represents DLsite content metadata.
/// This replaces the separate DLsiteMetadata.cs file.
/// </summary>
public class DLsiteMetadata : BaseMetadata
{
    /// <summary>
    /// Gets or sets the people information (creators, voice actors, etc.).
    /// </summary>
    public DLsitePerson[]? People { get; set; }
}

/// <summary>
/// Represents a person involved in DLsite content.
/// This replaces the separate DLsitePerson.cs file.
/// </summary>
public class DLsitePerson : BasePerson
{
}
#endregion

#region External ID
/// <summary>
/// External ID provider for DLsite content.
/// This replaces the separate DLsiteMovieExternalId.cs file.
/// </summary>
public class DLsiteMovieExternalId : BaseMovieId
{
    /// <inheritdoc />
    public override string ProviderName => "DLsite";

    /// <inheritdoc />
    public override string Key => "DLsite";
}
#endregion

#region Metadata Mapper
/// <summary>
/// DLsite metadata mapper.
/// Maps DLsite metadata to Jellyfin entities.
/// This replaces the separate DLsiteMetadataMapper.cs file.
/// </summary>
public class DLsiteMetadataMapper : BaseMetadataMapper<DLsiteMetadata, DLsitePerson, BaseSearchResult>
{
    private static readonly DLsiteMetadataMapper instance = new();

    /// <inheritdoc />
    protected override string ProviderIdKey => "DLsite";

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
            DLsiteExternalUrlStore.Instance.SetUrls(id, sourceUrls);
        }
    }

    /// <summary>
    /// Maps DLsite metadata to a Jellyfin Movie entity.
    /// </summary>
    /// <param name="metadata">The DLsite metadata.</param>
    /// <param name="movie">The movie entity to populate.</param>
    /// <param name="originalName">The original name of the content.</param>
    public static new void MapToMovie(DLsiteMetadata metadata, Movie movie, string? originalName = null)
    {
        ((BaseMetadataMapper<DLsiteMetadata, DLsitePerson, BaseSearchResult>)instance).MapToMovie(metadata, movie, originalName);
    }

    /// <summary>
    /// Maps a search result to a Jellyfin remote search result.
    /// </summary>
    /// <param name="searchResult">The search result to map.</param>
    /// <returns>The mapped remote search result.</returns>
    public static new RemoteSearchResult MapToSearchResult(BaseSearchResult searchResult)
    {
        return ((BaseMetadataMapper<DLsiteMetadata, DLsitePerson, BaseSearchResult>)instance).MapToSearchResult(searchResult);
    }

    /// <summary>
    /// Creates PersonInfo objects from DLsite metadata.
    /// </summary>
    /// <param name="metadata">The DLsite metadata.</param>
    /// <returns>Collection of PersonInfo objects.</returns>
    public static IEnumerable<PersonInfo> CreatePersonInfos(DLsiteMetadata metadata)
    {
        return metadata?.People != null
            ? instance.CreatePersonInfos(metadata.People)
            : Enumerable.Empty<PersonInfo>();
    }
}
#endregion

#region Unified API Client
/// <summary>
/// Unified HTTP client for communicating with the DLsite scraper backend service.
/// This replaces the separate DLsiteApiClient.cs file.
/// </summary>
public class DLsiteUnifiedApiClient : BaseScraperApiClient<DLsiteMetadata, BaseSearchResult>
{
    private readonly ILogger<DLsiteUnifiedApiClient> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DLsiteUnifiedApiClient"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="baseUrl">The base URL of the backend service.</param>
    /// <param name="apiToken">Optional API token for authentication.</param>
    public DLsiteUnifiedApiClient(ILogger<DLsiteUnifiedApiClient> logger, string baseUrl, string? apiToken = null)
        : base(logger, baseUrl, "dlsite", apiToken)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    protected override DLsiteMetadata ParseMetadata(JsonElement data, string id)
    {
        var metadata = new DLsiteMetadata();
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

        return results;
    }

    private BaseSearchResult? ParseSearchResult(JsonElement item)
    {
        if (!item.TryGetProperty("id", out var idProp) || string.IsNullOrWhiteSpace(idProp.GetString()))
        {
            return null;
        }

        var result = new BaseSearchResult();
        ParseBaseSearchResult(item, result);
        return result;
    }

    private DLsitePerson[] ParsePeople(JsonElement data)
    {
        if (data.TryGetProperty("people", out var peopleProp) && peopleProp.ValueKind == JsonValueKind.Array)
        {
            var people = new List<DLsitePerson>();
            foreach (var personElement in peopleProp.EnumerateArray())
            {
                var person = ParsePerson(personElement);
                if (person != null)
                {
                    people.Add(person);
                }
            }

            return people.ToArray();
        }

        return Array.Empty<DLsitePerson>();
    }

    private DLsitePerson? ParsePerson(JsonElement personElement)
    {
        if (!personElement.TryGetProperty("name", out var nameProp) || string.IsNullOrWhiteSpace(nameProp.GetString()))
        {
            return null;
        }

        return new DLsitePerson
        {
            Name = nameProp.GetString()!,
            Type = personElement.TryGetProperty("type", out var type) ? type.GetString() : null,
            Role = personElement.TryGetProperty("role", out var role) ? role.GetString() : null,
        };
    }
}
#endregion

#region Unified Metadata Provider
/// <summary>
/// Unified DLsite metadata provider.
/// This replaces the separate DLsiteMetadataProvider.cs file.
/// </summary>
public class DLsiteUnifiedMetadataProvider : BaseMetadataProviderService<DLsiteMetadata, DLsitePerson, DLsiteUnifiedApiClient, DLsiteMetadataMapper>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DLsiteUnifiedMetadataProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="apiClientFactory">The API client factory.</param>
    /// <param name="mapper">The metadata mapper.</param>
    public DLsiteUnifiedMetadataProvider(
        ILogger<DLsiteUnifiedMetadataProvider> logger,
        Func<DLsiteUnifiedApiClient> apiClientFactory,
        DLsiteMetadataMapper mapper)
        : base(logger, apiClientFactory, mapper)
    {
    }

    /// <inheritdoc />
    public override string Name => "DLsite";

    /// <inheritdoc />
    protected override string ProviderIdKey => "DLsite";

    /// <inheritdoc />
    protected override IEnumerable<DLsitePerson> GetPeople(DLsiteMetadata metadata)
    {
        return metadata.People ?? Array.Empty<DLsitePerson>();
    }

    /// <inheritdoc />
    protected override bool TryParseProviderId(string? input, out string id)
    {
        return DLsiteProviderHelpers.TryParseProviderId(input, out id);
    }
}
#endregion

#region Unified Image Provider
/// <summary>
/// Unified DLsite image provider.
/// This replaces the separate DLsiteImageProvider.cs file.
/// </summary>
public class DLsiteUnifiedImageProvider : BaseImageProviderService<DLsiteMetadata, DLsiteUnifiedApiClient>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DLsiteUnifiedImageProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="apiClientFactory">The API client factory.</param>
    public DLsiteUnifiedImageProvider(ILogger<DLsiteUnifiedImageProvider> logger, Func<DLsiteUnifiedApiClient> apiClientFactory)
        : base(logger, apiClientFactory)
    {
    }

    /// <inheritdoc />
    public override string Name => "DLsite";

    /// <inheritdoc />
    protected override string ProviderIdKey => "DLsite";

    /// <inheritdoc />
    protected override bool TryParseProviderId(string? input, out string id)
    {
        return DLsiteProviderHelpers.TryParseProviderId(input, out id);
    }
}
#endregion

#region Unified External URL Provider
/// <summary>
/// Unified DLsite external URL provider.
/// This replaces the separate DLsiteExternalUrlProvider.cs file.
/// </summary>
public class DLsiteUnifiedExternalUrlProvider : BaseExternalUrlProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DLsiteUnifiedExternalUrlProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public DLsiteUnifiedExternalUrlProvider(ILogger<DLsiteUnifiedExternalUrlProvider> logger)
        : base(DLsiteExternalUrlStore.Instance, "DLsite")
    {
    }

    /// <inheritdoc />
    public override string Name => "DLsite";

    /// <inheritdoc />
    public override string UrlFormatString => "https://www.dlsite.com/maniax/work/=/product_id/{0}.html";
}
#endregion

#region Provider Helpers
/// <summary>
/// Shared helper methods for DLsite provider components.
/// Centralizes common logic to avoid code duplication.
/// </summary>
internal static class DLsiteProviderHelpers
{
    /// <summary>
    /// Attempts to parse a DLsite provider ID from various input formats.
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
        
        // DLsite ID format (RJ123456 or VJ123456)
        var dlsiteIdRegex = new Regex(@"^(RJ|VJ)\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (dlsiteIdRegex.IsMatch(cleaned))
        {
            id = cleaned.ToUpperInvariant();
            return true;
        }

        // ID in URL
        var urlIdRegex = new Regex(@"product_id/((?:RJ|VJ)\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var urlMatch = urlIdRegex.Match(cleaned);
        if (urlMatch.Success)
        {
            id = urlMatch.Groups[1].Value.ToUpperInvariant();
            return true;
        }

        return false;
    }
}
#endregion
