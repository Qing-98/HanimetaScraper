using System;
using System.Collections.Generic;
using System.Text.Json;
using Jellyfin.Plugin.Hanimeta.Common.Client;
using Jellyfin.Plugin.Hanimeta.Common.Extensions;
using Jellyfin.Plugin.Hanimeta.Common.Models;
using Jellyfin.Plugin.Hanimeta.DLsiteScraper.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.DLsiteScraper.Client
{
    /// <summary>
    /// HTTP client for communicating with the DLsite scraper backend service.
    /// </summary>
    public class DLsiteApiClient : BaseScraperApiClient<DLsiteMetadata, BaseSearchResult>
    {
        private readonly ILogger<DLsiteApiClient> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DLsiteApiClient"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="baseUrl">The base URL of the backend service.</param>
        /// <param name="apiToken">The optional API token for authentication.</param>
        public DLsiteApiClient(ILogger<DLsiteApiClient> logger, string baseUrl, string? apiToken = null)
            : base(logger, baseUrl, "dlsite", apiToken)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Parse metadata from JSON data.
        /// </summary>
        /// <param name="data">The JSON data.</param>
        /// <param name="id">The content ID.</param>
        /// <returns>The parsed metadata.</returns>
        protected override DLsiteMetadata ParseMetadata(JsonElement data, string id)
        {
            var metadata = new DLsiteMetadata();

            // Use base class method to parse common fields
            this.ParseBaseMetadata(data, id, metadata);

            // Parse DLsite-specific people data
            metadata.People = this.ParsePeople(data);

            return metadata;
        }

        /// <summary>
        /// Parse search results from JSON data.
        /// </summary>
        /// <param name="data">The JSON data.</param>
        /// <returns>Collection of search results.</returns>
        protected override IEnumerable<BaseSearchResult> ParseSearchResults(JsonElement data)
        {
            var results = new List<BaseSearchResult>();

            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var result = this.ParseSearchResult(item);
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

            // Use base class method to parse common fields
            this.ParseBaseSearchResult(item, result);

            return result;
        }

        private DLsitePerson[] ParsePeople(JsonElement data)
        {
            if (data.TryGetProperty("people", out var peopleProp) && peopleProp.ValueKind == JsonValueKind.Array)
            {
                var people = new List<DLsitePerson>();
                foreach (var personElement in peopleProp.EnumerateArray())
                {
                    var person = this.ParsePerson(personElement);
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
}
