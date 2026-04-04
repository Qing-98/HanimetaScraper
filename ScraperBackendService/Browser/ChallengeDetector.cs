using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ScraperBackendService.Browser;

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

        var analysis = AnalyzeChallengePage(html);
        if (!analysis.IsChallenge)
        {
            return false;
        }

        _logger.LogDebug("Challenge detected ({Reasons}): {Url}", string.Join(", ", analysis.Reasons), url);
        return true;
    }

    public ChallengeAnalysis AnalyzeChallengePage(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return new ChallengeAnalysis(false, []);
        }

        if (TryGetDefinitiveChallengeReasons(html, out var definitiveReasons))
        {
            return new ChallengeAnalysis(true, definitiveReasons);
        }

        if (TryGetCombinedChallengeReasons(html, out var combinedReasons))
        {
            return new ChallengeAnalysis(true, combinedReasons);
        }

        if (TryGetSuspiciousChallengeReasons(html, out var suspiciousReasons))
        {
            return new ChallengeAnalysis(true, suspiciousReasons);
        }

        return new ChallengeAnalysis(false, []);
    }

    private bool TryGetDefinitiveChallengeReasons(string html, out string[] reasons)
    {
        var matched = new List<string>();

        var definitivePatterns = new (string Pattern, string Reason)[]
        {
            ("<title>Just a moment...</title>", "definitive:title-just-a-moment"),
            ("<title>Attention Required! | Cloudflare</title>", "definitive:title-attention-required"),
            ("id=\"challenge-form\"", "definitive:challenge-form-id"),
            ("class=\"cf-challenge-form\"", "definitive:cf-challenge-form-class"),
            ("/cdn-cgi/challenge-platform/h/", "definitive:cdn-cgi-challenge-script"),
            ("window._cf_chl_opt", "definitive:cf-chl-opt-script"),
            ("<div id=\"cf-wrapper\">", "definitive:cf-wrapper"),
            ("<div class=\"cf-browser-verification cf-im-under-attack\">", "definitive:cf-browser-verification")
        };

        foreach (var (pattern, reason) in definitivePatterns)
        {
            if (html.Contains(pattern, StringComparison.Ordinal))
            {
                matched.Add(reason);
            }
        }

        reasons = matched.ToArray();
        return reasons.Length > 0;
    }

    private bool TryGetCombinedChallengeReasons(string html, out string[] reasons)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var bodyText = doc.DocumentNode.SelectSingleNode("//body")?.InnerText?.Trim() ?? "";

        var hasChallengeText =
            bodyText.Contains("Checking your browser", StringComparison.OrdinalIgnoreCase) ||
            bodyText.Contains("Verifying you are human", StringComparison.OrdinalIgnoreCase) ||
            bodyText.Contains("Please enable cookies", StringComparison.OrdinalIgnoreCase);

        var hasRayId =
            bodyText.Contains("Ray ID:", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("data-ray", StringComparison.OrdinalIgnoreCase);

        var hasChallengeDom =
            doc.DocumentNode.SelectSingleNode("//div[@id='challenge-error-title']") != null ||
            doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'cf-error-details')]") != null;

        var matched = new List<string>();
        if (hasChallengeText) matched.Add("combined:challenge-text");
        if (hasRayId) matched.Add("combined:ray-id");
        if (hasChallengeDom) matched.Add("combined:challenge-dom");

        if (matched.Count >= 2)
        {
            reasons = matched.ToArray();
            return true;
        }

        reasons = [];
        return false;
    }

    private bool TryGetSuspiciousChallengeReasons(string html, out string[] reasons)
    {
        if (html.TrimStart().StartsWith("{") || html.TrimStart().StartsWith("["))
        {
            reasons = [];
            return false;
        }

        if (html.Length > 5000)
        {
            reasons = [];
            return false;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var bodyText = doc.DocumentNode.SelectSingleNode("//body")?.InnerText?.Trim() ?? "";

        var hasChallengeForm =
            html.Contains("challenge-form", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("cf-challenge", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("cf_clearance", StringComparison.OrdinalIgnoreCase) ||
            doc.DocumentNode.SelectSingleNode("//form[@id='challenge-form']") != null ||
            doc.DocumentNode.SelectSingleNode("//div[@class='cf-challenge']") != null;

        var hasRayId =
            bodyText.Contains("Ray ID:", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("data-ray", StringComparison.OrdinalIgnoreCase);

        var hasJustAMoment = bodyText.Contains("Just a moment", StringComparison.OrdinalIgnoreCase) ||
                            bodyText.Contains("Checking your browser", StringComparison.OrdinalIgnoreCase);

        var isVeryShort = bodyText.Length < 300;

        if (hasChallengeForm && hasRayId && hasJustAMoment && isVeryShort)
        {
            reasons = [
                "suspicious:challenge-form",
                "suspicious:ray-id",
                "suspicious:challenge-message",
                "suspicious:very-short-body"
            ];
            return true;
        }

        reasons = [];
        return false;
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

public sealed record ChallengeAnalysis(bool IsChallenge, IReadOnlyList<string> Reasons);
