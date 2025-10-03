using HtmlAgilityPack;
using ScraperBackendService.Core.Normalize;
using ScraperBackendService.Core.Util;
using ScraperBackendService.Models;

namespace ScraperBackendService.Core.Parsing;

/// <summary>
/// Personnel extraction utilities for parsing staff information from content pages.
/// Handles role mapping, deduplication, and structured personnel data extraction.
/// </summary>
/// <example>
/// Usage examples:
///
/// // Extract all personnel from DLsite page
/// PeopleEx.ExtractDLsitePersonnel(htmlDocument, metadata);
///
/// // Add individual person with role mapping
/// PeopleEx.AddPersonWithOriginalRole(metadata, "山田太郎", "声優");
///
/// // Manual role mapping
/// var (englishType, role) = PeopleEx.MapStaffRole("監督");
/// // Returns: ("Director", null)
/// </example>
public static class PeopleEx
{
    /// <summary>
    /// Maps Japanese role names to standardized English types.
    /// Uses ScrapingUtils.MapStaffRole for consistent role normalization.
    /// </summary>
    /// <param name="header">Japanese role name (e.g., "声優", "監督")</param>
    /// <returns>Tuple containing normalized English type and optional role detail</returns>
    /// <example>
    /// var (type, role) = MapStaffRole("声優");
    /// // Returns: ("Actor", null)
    ///
    /// var (type2, role2) = MapStaffRole("監督");
    /// // Returns: ("Director", null)
    ///
    /// var (type3, role3) = MapStaffRole("シナリオ");
    /// // Returns: ("Writer", null)
    /// </example>
    public static (string Type, string? Role) MapStaffRole(string header)
        => ScrapingUtils.MapStaffRole(header);

    /// <summary>
    /// Adds a person to metadata with automatic deduplication.
    /// Stores both normalized English type and original role for reference.
    /// </summary>
    /// <param name="meta">Metadata object to add person to</param>
    /// <param name="name">Person's name</param>
    /// <param name="type">Normalized English role type (Actor, Director, etc.)</param>
    /// <param name="originalRole">Original role name in source language</param>
    /// <example>
    /// // Add voice actor with original Japanese role
    /// AddPerson(metadata, "田中花音", "Actor", "声優");
    ///
    /// // Add director with original role
    /// AddPerson(metadata, "佐藤一郎", "Director", "監督");
    ///
    /// // Add writer
    /// AddPerson(metadata, "鈴木次郎", "Writer", "シナリオ");
    /// </example>
    public static void AddPerson(Metadata meta, string name, string type, string? originalRole)
        => ScrapingUtils.AddPerson(meta, name, type, originalRole);

    /// <summary>
    /// Extracts personnel information from DLsite work_outline table.
    /// Scans table rows for personnel-related roles and extracts associated names.
    /// Only processes roles that can be mapped to standard English types.
    /// </summary>
    /// <param name="doc">HTML document containing the DLsite page</param>
    /// <param name="meta">Metadata object to populate with personnel information</param>
    /// <example>
    /// var doc = new HtmlDocument();
    /// doc.LoadHtml(htmlContent);
    /// var metadata = new HanimeMetadata();
    ///
    /// ExtractDLsitePersonnel(doc, metadata);
    ///
    /// // Results in metadata.People containing extracted personnel:
    /// // - Voice actors (声優 -> Actor)
    /// // - Directors (監督 -> Director)
    /// // - Writers (シナリオ -> Writer)
    /// // - Other recognized roles
    ///
    /// Console.WriteLine($"Found {metadata.People.Count} personnel entries");
    /// foreach (var person in metadata.People)
    /// {
    ///     Console.WriteLine($"{person.Name} - {person.Type} ({person.Role})");
    /// }
    /// </example>
    public static void ExtractDLsitePersonnel(HtmlDocument doc, Metadata meta)
    {
        // Iterate through all rows in the work_outline table
        var rows = doc.DocumentNode.SelectNodes("//table[@id='work_outline']//tr");
        if (rows == null) return;

        foreach (var tr in rows)
        {
            var th = tr.SelectSingleNode(".//th");
            var td = tr.SelectSingleNode(".//td");
            if (th == null || td == null) continue;

            var roleRaw = TextNormalizer.Clean(th.InnerText);
            if (string.IsNullOrWhiteSpace(roleRaw)) continue;

            // Use MapStaffRole to check if this is a personnel-related role
            var (mappedType, _) = MapStaffRole(roleRaw);

            // If mapped type equals original role, it's not a recognized personnel role
            if (mappedType.Equals(roleRaw, StringComparison.OrdinalIgnoreCase))
                continue;

            // Extract person names (prefer from links, fallback to cell text)
            var nameNodes = td.SelectNodes(".//a");
            var names = nameNodes?.Select(x => TextNormalizer.Clean(x.InnerText))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList()
                ?? new List<string> { TextNormalizer.Clean(td.InnerText) };

            // Add personnel information: Type = normalized English role, Role = original Japanese role
            foreach (var name in names)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    AddPerson(meta, name, mappedType, roleRaw);
                }
            }
        }
    }

    /// <summary>
    /// Adds a person with automatic role mapping from original Japanese role name.
    /// Convenience method that combines role mapping and person addition.
    /// </summary>
    /// <param name="meta">Metadata object to add person to</param>
    /// <param name="name">Person's name</param>
    /// <param name="originalRoleJapanese">Original Japanese role name</param>
    /// <example>
    /// // Add voice actor using Japanese role
    /// AddPersonWithOriginalRole(metadata, "田中花音", "声優");
    /// // Automatically maps "声優" to "Actor" type
    ///
    /// // Add director using Japanese role
    /// AddPersonWithOriginalRole(metadata, "佐藤監督", "監督");
    /// // Automatically maps "監督" to "Director" type
    ///
    /// // Skip unrecognized roles
    /// AddPersonWithOriginalRole(metadata, "Unknown Person", "UnknownRole");
    /// // Will be skipped since "UnknownRole" doesn't map to a standard type
    /// </example>
    public static void AddPersonWithOriginalRole(Metadata meta, string name, string originalRoleJapanese)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(originalRoleJapanese))
            return;

        var (normalizedType, _) = MapStaffRole(originalRoleJapanese);

        // If mapped type equals original role, it's not a recognized personnel role - skip
        if (normalizedType.Equals(originalRoleJapanese, StringComparison.OrdinalIgnoreCase))
            return;

        AddPerson(meta, name, normalizedType, originalRoleJapanese);
    }
}
