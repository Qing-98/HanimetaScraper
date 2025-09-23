using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScraperBackendService.AntiCloudflare;
using ScraperBackendService.Core.Net;

// 演示合并后的 PlaywrightNetworkClient 的两种使用方式

class PlaywrightClientDemo
{
    public static async Task DemoSimpleMode()
    {
        Console.WriteLine("=== 简单模式演示 ===");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });

        // 简单模式：每次创建新 Context
        var client = new PlaywrightNetworkClient(browser);

        var html = await client.GetHtmlAsync("https://example.com", CancellationToken.None);
        Console.WriteLine($"获取到 HTML，长度: {html.Length}");
    }

    public static async Task DemoContextManagerMode()
    {
        Console.WriteLine("=== Context 管理模式演示 ===");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<PlaywrightNetworkClient>();

        // Context 管理模式：使用 PlaywrightContextManager
        var contextManager = new PlaywrightContextManager(browser, logger);
        var client = new PlaywrightNetworkClient(contextManager, logger);

        var html = await client.GetHtmlAsync("https://example.com", CancellationToken.None);
        Console.WriteLine($"获取到 HTML，长度: {html.Length}");
    }
}
