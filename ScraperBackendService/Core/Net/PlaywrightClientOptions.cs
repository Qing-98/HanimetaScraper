namespace ScraperBackendService.Core.Net;

/// <summary>
/// Unified browser client configuration options, integrating PlaywrightClientOptions and ScrapeRuntimeOptions
/// </summary>
public sealed class PlaywrightClientOptions
{
    // ================= Basic Browser Configuration =================
    public string UserAgent { get; init; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";
    public string Locale { get; init; } = "zh-CN";
    public string TimezoneId { get; init; } = "Australia/Melbourne";
    public string AcceptLanguage { get; init; } = "zh-CN,zh;q=0.9";
    public int ViewportWidth { get; init; } = 1280;
    public int ViewportHeight { get; init; } = 900;

    // ================= Timeout Configuration =================
    public int GotoTimeoutMs { get; init; } = 60_000;
    public int WaitSelectorTimeoutMs { get; init; } = 15_000;
    public int SlowRetryGotoTimeoutMs { get; init; } = 90_000;
    public int SlowRetryWaitSelectorMs { get; init; } = 30_000;

    // ================= Script and Selector Configuration =================
    /// <summary>Init script path loaded in each context (e.g., stealth script). Can be null.</summary>
    public string? InitScriptPath { get; init; } = Path.Combine("AntiCloudflare", "StealthInit.js");

    /// <summary>Optional selectors to wait for after opening page (any match is sufficient).</summary>
    public string[] ReadySelectors { get; init; } = new[] { "body" };

    // ================= Context Management Configuration =================
    /// <summary>Context TTL in minutes</summary>
    public int ContextTtlMinutes { get; init; } = 8;

    /// <summary>Maximum pages per context</summary>
    public int MaxPagesPerContext { get; init; } = 50;

    /// <summary>Whether to rotate context when challenge is detected</summary>
    public bool RotateOnChallengeDetected { get; init; } = true;

    // ================= Challenge Detection Configuration =================
    /// <summary>Challenge detection: URLs containing these keywords are considered challenge pages</summary>
    public string[] ChallengeUrlHints { get; init; } = new[]
    {
        "/cdn-cgi/challenge-platform/",
        "/challenge-platform/",
        "checking-your-browser",
        "ddos-protection",
        "challenge",
        "cf-challenge",
        "cloudflare",
        "/cdn-cgi/"
    };

    /// <summary>Challenge detection: DOM containing these keywords are considered challenge pages</summary>
    public string[] ChallengeDomHints { get; init; } = new[]
    {
        "Checking your browser",
        "DDoS protection",
        "cf-browser-verification",
        "challenge-form",
        "cf-challenge",
        "#challenge-form",
        "Just a moment",
        "Verifying you are human"
    };

    // ================= Isolation Mode Configuration =================
    /// <summary>Context isolation mode</summary>
    public ContextIsolationMode IsolationMode { get; init; } = ContextIsolationMode.Shared;
}

/// <summary>
/// Context isolation mode
/// </summary>
public enum ContextIsolationMode
{
    Shared,            // Search and detail share the same context (default)
    SplitSearchDetail  // Search and detail are separated, detail context can be rotated independently
}
