using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ScraperBackendService.AntiCloudflare;

/// <summary>
/// Improved Cloudflare challenge detector with reduced false positive rate.
/// Uses multi-layer detection strategy to accurately identify challenge pages.
/// </summary>
public class ChallengeDetector
{
    private readonly ILogger _logger;
    
    public ChallengeDetector(ILogger logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Detects if the page is a Cloudflare challenge page.
    /// Uses three-layer detection strategy with increasing specificity.
    /// </summary>
    /// <param name="html">HTML content to analyze</param>
    /// <param name="url">URL for logging context</param>
    /// <returns>True if challenge is detected, false otherwise</returns>
    public bool IsCloudflareChallengePage(string html, string? url = null)
    {
        if (string.IsNullOrEmpty(html)) return false;
        
        // Layer 1: Check for definitive challenge signatures (highest confidence)
        if (HasDefinitiveChallengeSignature(html))
        {
            _logger.LogDebug("Definitive challenge signature detected: {Url}", url);
            return true;
        }
        
        // Layer 2: Check for combined challenge indicators (medium confidence)
        if (HasCombinedChallengeIndicators(html, url))
        {
            _logger.LogDebug("Combined challenge indicators detected: {Url}", url);
            return true;
        }
        
        // Layer 3: Check for suspicious patterns (low confidence, strict conditions)
        if (HasSuspiciousChallengePattern(html))
        {
            _logger.LogDebug("Suspicious challenge pattern detected: {Url}", url);
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Layer 1: Definitive challenge signatures (must be very specific).
    /// These patterns are unmistakable indicators of a challenge page.
    /// </summary>
    private bool HasDefinitiveChallengeSignature(string html)
    {
        // Very specific patterns that only appear on challenge pages
        var definitivePatterns = new[]
        {
            // Exact title patterns
            "<title>Just a moment...</title>",
            "<title>Attention Required! | Cloudflare</title>",
            
            // Specific form and element IDs
            "id=\"challenge-form\"",
            "class=\"cf-challenge-form\"",
            
            // Specific script paths
            "/cdn-cgi/challenge-platform/h/",
            "window._cf_chl_opt",
            
            // Specific containers
            "<div id=\"cf-wrapper\">",
            "<div class=\"cf-browser-verification cf-im-under-attack\">"
        };
        
        return definitivePatterns.Any(p => html.Contains(p, StringComparison.Ordinal));
    }
    
    /// <summary>
    /// Layer 2: Combined indicators (requires multiple conditions).
    /// Reduces false positives by requiring at least 2 indicators.
    /// </summary>
    private bool HasCombinedChallengeIndicators(string html, string? url)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        var bodyText = doc.DocumentNode.SelectSingleNode("//body")?.InnerText?.Trim() ?? "";
        
        // Check for challenge-specific text
        var hasChallengeText = 
            bodyText.Contains("Checking your browser", StringComparison.OrdinalIgnoreCase) ||
            bodyText.Contains("Verifying you are human", StringComparison.OrdinalIgnoreCase) ||
            bodyText.Contains("Please enable cookies", StringComparison.OrdinalIgnoreCase);
        
        // Check for Cloudflare Ray ID
        var hasRayId = 
            bodyText.Contains("Ray ID:", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("data-ray", StringComparison.OrdinalIgnoreCase);
        
        // Check for challenge DOM elements
        var hasChallengeDom = 
            doc.DocumentNode.SelectSingleNode("//div[@id='challenge-error-title']") != null ||
            doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'cf-error-details')]") != null;
        
        // Count how many indicators are present
        var indicatorCount = 0;
        if (hasChallengeText) indicatorCount++;
        if (hasRayId) indicatorCount++;
        if (hasChallengeDom) indicatorCount++;
        
        // Require at least 2 indicators to reduce false positives
        return indicatorCount >= 2;
    }
    
    /// <summary>
    /// Layer 3: Suspicious patterns (very strict conditions).
    /// Only triggers for very short pages with all required conditions.
    /// </summary>
    private bool HasSuspiciousChallengePattern(string html)
    {
        // Only check very short pages to avoid false positives
        if (html.Length > 5000) return false;
        
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        var bodyText = doc.DocumentNode.SelectSingleNode("//body")?.InnerText?.Trim() ?? "";
        
        // All conditions must be met
        var hasJustAMoment = bodyText.Contains("Just a moment", StringComparison.OrdinalIgnoreCase);
        var hasCloudflareScript = html.Contains("cloudflare", StringComparison.OrdinalIgnoreCase);
        var hasRayId = bodyText.Contains("Ray ID:", StringComparison.OrdinalIgnoreCase);
        var isVeryShort = bodyText.Length < 500;
        
        return hasJustAMoment && hasCloudflareScript && hasRayId && isVeryShort;
    }
    
    /// <summary>
    /// Verifies that the page has actual content (used after challenge resolution).
    /// Checks for presence of meaningful content elements.
    /// </summary>
    public async Task<bool> HasValidContentAsync(IPage page)
    {
        try
        {
            // Check title
            var title = await page.TitleAsync();
            if (string.IsNullOrWhiteSpace(title) || 
                title.Contains("Just a moment", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            
            // Check body text length
            var bodyText = await page.EvaluateAsync<string>(
                "() => document.body?.innerText || ''");
            
            if (bodyText.Length < 100)
            {
                return false;
            }
            
            // Check for actual content elements
            var contentSelectors = new[]
            {
                "main", "article", "#content", ".content",
                "video", "img[src]", ".video-player",
                "#shareBtn-title",    // Hanime specific
                "#work_name",         // DLsite specific
                "div[title]",         // Hanime search results
                "#search_result_list" // DLsite search results
            };
            
            foreach (var selector in contentSelectors)
            {
                try
                {
                    var count = await page.Locator(selector).CountAsync();
                    if (count > 0)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore selector errors, try next
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error verifying page content");
            return false;
        }
    }
}
