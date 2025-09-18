namespace ScraperBackendService.Core.Net;

public sealed class PlaywrightClientOptions
{
    public string UserAgent { get; init; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118 Safari/537.36";
    public string Locale { get; init; } = "en-US";
    public string TimezoneId { get; init; } = "UTC";
    public string AcceptLanguage { get; init; } = "en-US,en;q=0.9";
    public int ViewportWidth { get; init; } = 1366;
    public int ViewportHeight { get; init; } = 900;

    public int GotoTimeoutMs { get; init; } = 60_000;
    public int WaitSelectorTimeoutMs { get; init; } = 15_000;

    /// <summary>在每个 Context 加载的 init 脚本路径（如 stealth 脚本）。可为空。</summary>
    public string? InitScriptPath { get; init; }

    /// <summary>打开页面后可选等待的选择器（任一命中即可）。</summary>
    public string[] ReadySelectors { get; init; } = new[] { "body" };
}
