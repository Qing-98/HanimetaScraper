using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using ScraperBackendService.Models;
using ScraperBackendService.Services;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Hanime Scraper Console Test ===");

        // 启动 Playwright 浏览器
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false // 改成 false 可以看浏览器界面
        });

        var client = new HanimeScraperPlaywrightClient(
            browser,
            NullLogger<HanimeScraperPlaywrightClient>.Instance
        );

        // 几个简单测试用例
        var testInputs = new List<(string Input, HanimeScrapeRoute Route)>
        {
            //("86994", HanimeScrapeRoute.ById),
            //("https://hanime1.me/watch?v=86994", HanimeScrapeRoute.Auto),
            ("Nurse", HanimeScrapeRoute.ByFilename),
            ("Love", HanimeScrapeRoute.Auto)
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        foreach (var (input, route) in testInputs)
        {
            Console.WriteLine($"\n>>> 测试输入: {input} | 模式: {route}");

            try
            {
                var results = await client.FetchAsync(input, route, maxResults: 3, sort: "最新上市", ct: cts.Token);

                if (results.Count == 0)
                {
                    Console.WriteLine("⚠️  未抓取到任何结果！");
                    continue;
                }

                foreach (var meta in results)
                {
                    Console.WriteLine($"- ID: {meta.ID}");
                    Console.WriteLine($"  Title: {(string.IsNullOrWhiteSpace(meta.Title) ? "❌空" : meta.Title)}");
                    Console.WriteLine($"  Description: {(string.IsNullOrWhiteSpace(meta.Description) ? "❌空" : meta.Description[..Math.Min(60, meta.Description.Length)] + "...")}");
                    Console.WriteLine($"  Tags: {(meta.Genres == null || meta.Genres.Count == 0 ? "❌空" : string.Join(", ", meta.Genres))}");
                    Console.WriteLine($"  URL: {string.Join(", ", meta.SourceUrls)}");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 抓取失败: {ex.Message}");
            }
        }

        Console.WriteLine("\n=== 测试完成 ===");
    }
}
