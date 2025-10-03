using ScraperBackendService.Models;

namespace ScraperBackendService.Core.Util;

/// <summary>
/// Unified test result display utilities
/// </summary>
public static class TestDisplayUtils
{
    /// <summary>
    /// Display detailed metadata information
    /// </summary>
    public static void DisplayDetailedMetadata(Metadata meta, int index)
    {
        Console.WriteLine($"📋 Result #{index}:");
        Console.WriteLine($"   🏷️  ID: {meta.ID ?? "❌Empty"}");
        Console.WriteLine($"   📝 Title: {meta.Title ?? "❌Empty"}");

        if (!string.IsNullOrWhiteSpace(meta.OriginalTitle) && meta.OriginalTitle != meta.Title)
            Console.WriteLine($"   📝 Original Title: {meta.OriginalTitle}");

        if (!string.IsNullOrWhiteSpace(meta.Description))
        {
            var desc = meta.Description.Length > 100
                ? meta.Description[..100] + "..."
                : meta.Description;
            Console.WriteLine($"   📄 Description: {desc}");
        }
        else
        {
            Console.WriteLine($"   📄 Description: ❌Empty");
        }

        Console.WriteLine($"   ⭐ Rating: {(meta.Rating.HasValue ? $"{meta.Rating:F1}/5.0" : "❌Empty")}");

        if (meta.ReleaseDate.HasValue)
            Console.WriteLine($"   📅 Release Date: {meta.ReleaseDate.Value:yyyy-MM-dd}");
        else
            Console.WriteLine($"   📅 Release Date: ❌Empty");

        if (meta.Year.HasValue)
            Console.WriteLine($"   📅 Year: {meta.Year}");

        // Studios
        if (meta.Studios.Count > 0)
            Console.WriteLine($"   🏢 Studios: {string.Join(", ", meta.Studios)}");
        else
            Console.WriteLine($"   🏢 Studios: ❌Empty");

        // Series
        if (meta.Series.Count > 0)
            Console.WriteLine($"   📚 Series: {string.Join(", ", meta.Series)}");
        else
            Console.WriteLine($"   📚 Series: ❌Empty");

        // Tags/Genres
        if (meta.Genres.Count > 0)
            Console.WriteLine($"   🏷️  Tags: {string.Join(", ", meta.Genres)}");
        else
            Console.WriteLine($"   🏷️  Tags: ❌Empty");

        // People
        if (meta.People.Count > 0)
        {
            Console.WriteLine($"   👥 People ({meta.People.Count}):");
            foreach (var person in meta.People.Take(5)) // Show only first 5
            {
                var roleInfo = !string.IsNullOrWhiteSpace(person.Role) ? $" ({person.Role})" : "";
                Console.WriteLine($"      • {person.Name} - {person.Type}{roleInfo}");
            }
            if (meta.People.Count > 5)
                Console.WriteLine($"      ... and {meta.People.Count - 5} more");
        }
        else
        {
            Console.WriteLine($"   👥 People: ❌Empty");
        }

        // Images - Detailed display
        DisplayImageDetails(meta);

        // Source URLs
        if (meta.SourceUrls.Count > 0)
            Console.WriteLine($"   🔗 Source URLs: {string.Join(", ", meta.SourceUrls)}");
        else
            Console.WriteLine($"   🔗 Source URLs: ❌Empty");
    }

    /// <summary>
    /// Display detailed image information
    /// </summary>
    private static void DisplayImageDetails(Metadata meta)
    {
        var hasAnyImage = !string.IsNullOrWhiteSpace(meta.Primary) ||
                         !string.IsNullOrWhiteSpace(meta.Backdrop) ||
                         meta.Thumbnails.Count > 0;

        if (!hasAnyImage)
        {
            Console.WriteLine($"   🖼️  Images: ❌No images found");
            return;
        }

        Console.WriteLine($"   🖼️  Images Found:");

        // Primary Image
        if (!string.IsNullOrWhiteSpace(meta.Primary))
        {
            Console.WriteLine($"      🎯 Primary: {TruncateUrl(meta.Primary)}");
        }

        // Backdrop Image
        if (!string.IsNullOrWhiteSpace(meta.Backdrop))
        {
            Console.WriteLine($"      🌄 Backdrop: {TruncateUrl(meta.Backdrop)}");
        }

        // Thumbnails
        if (meta.Thumbnails.Count > 0)
        {
            Console.WriteLine($"      📸 Thumbnails ({meta.Thumbnails.Count}):");
            for (int i = 0; i < Math.Min(meta.Thumbnails.Count, 10); i++) // Show max 10 thumbnails
            {
                Console.WriteLine($"         [{i + 1:D2}] {TruncateUrl(meta.Thumbnails[i])}");
            }

            if (meta.Thumbnails.Count > 10)
            {
                Console.WriteLine($"         ... and {meta.Thumbnails.Count - 10} more thumbnails");
            }
        }

        // Summary
        var totalImages = (string.IsNullOrWhiteSpace(meta.Primary) ? 0 : 1) +
                         (string.IsNullOrWhiteSpace(meta.Backdrop) ? 0 : 1) +
                         meta.Thumbnails.Count;
        Console.WriteLine($"      📊 Total Images: {totalImages}");
    }

    /// <summary>
    /// Truncate long URLs for display
    /// </summary>
    private static string TruncateUrl(string url, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(url)) return "❌Empty";

        if (url.Length <= maxLength) return url;

        // Try to keep the filename visible
        var lastSlash = url.LastIndexOf('/');
        if (lastSlash > 0 && url.Length - lastSlash < maxLength / 2)
        {
            var prefixLength = maxLength - (url.Length - lastSlash) - 3; // 3 for "..."
            if (prefixLength > 10)
            {
                return url[..prefixLength] + "..." + url[lastSlash..];
            }
        }

        // Fallback: simple truncation
        return url[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Display simplified metadata information with basic image info
    /// </summary>
    public static void DisplaySimpleMetadata(Metadata meta)
    {
        Console.WriteLine($"- ID: {meta.ID}");
        Console.WriteLine($"  Title: {(string.IsNullOrWhiteSpace(meta.Title) ? "❌Empty" : meta.Title)}");

        if (!string.IsNullOrWhiteSpace(meta.Description))
        {
            var desc = meta.Description.Length > 60 ? meta.Description[..60] + "..." : meta.Description;
            Console.WriteLine($"  Description: {desc}");
        }
        else
        {
            Console.WriteLine($"  Description: ❌Empty");
        }

        Console.WriteLine($"  Tags: {(meta.Genres?.Count > 0 ? string.Join(", ", meta.Genres) : "❌Empty")}");

        // Add image count summary
        var imageCount = (string.IsNullOrWhiteSpace(meta.Primary) ? 0 : 1) +
                        (string.IsNullOrWhiteSpace(meta.Backdrop) ? 0 : 1) +
                        meta.Thumbnails.Count;
        Console.WriteLine($"  Images: {(imageCount > 0 ? $"✅{imageCount} found" : "❌None")}");

        Console.WriteLine($"  URL: {string.Join(", ", meta.SourceUrls)}");
        Console.WriteLine();
    }

    /// <summary>
    /// Display test result summary
    /// </summary>
    public static void DisplayTestSummary(string provider, string input, int resultCount, TimeSpan elapsed)
    {
        Console.WriteLine($"📊 Test Summary - {provider}");
        Console.WriteLine($"   🔍 Search Term: {input}");
        Console.WriteLine($"   📋 Result Count: {resultCount}");
        Console.WriteLine($"   ⏱️ Duration: {elapsed.TotalSeconds:F1} seconds");
    }

    /// <summary>
    /// Display error information
    /// </summary>
    public static void DisplayError(string operation, Exception ex)
    {
        Console.WriteLine($"❌ {operation} failed: {ex.Message}");
        if (ex.InnerException != null)
            Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
    }

    /// <summary>
    /// Display separator line
    /// </summary>
    public static void DisplaySeparator(char character = '=', int length = 80)
    {
        Console.WriteLine("".PadRight(length, character));
    }

    /// <summary>
    /// Display progress information
    /// </summary>
    public static void DisplayProgress(string operation, int current, int total)
    {
        var percentage = (double)current / total * 100;
        Console.WriteLine($"🔄 {operation}: {current}/{total} ({percentage:F0}%)");
    }
}
