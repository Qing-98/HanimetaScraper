using Microsoft.Playwright;
using ScraperBackendService.Models;
using ScraperBackendService.Services;

var builder = WebApplication.CreateBuilder(args);

// 1) 确保安装浏览器（首次运行会下载）
//await Playwright.InstallAsync();
var playwright = await Playwright.CreateAsync();
var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = true
});

// 2) 单例 Playwright/Browser，供服务复用
builder.Services.AddSingleton(async sp =>
{
    var pw = await Playwright.CreateAsync();
    var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
        Headless = true,
        Args = new[] { "--no-sandbox", "--disable-blink-features=AutomationControlled" }
    });
    return browser;
});

// 3) 注册 Hanime 刮削服务（依赖单例 Browser）
builder.Services.AddScoped<HanimeScraperPlaywrightClient>(sp =>
{
    var browser = sp.GetRequiredService<Task<IBrowser>>().GetAwaiter().GetResult();
    var logger = sp.GetRequiredService<ILogger<HanimeScraperPlaywrightClient>>();
    return new HanimeScraperPlaywrightClient(browser, logger);
});

builder.Services.AddLogging();

var app = builder.Build();

// 健康检查
app.MapGet("/", () => Results.Ok(new { ok = true, service = "ScraperBackendService", ts = DateTime.UtcNow }));

// =============== Hanime ===============
// 搜索：/api/hanime/search?title=xxx&max=12&genre=裏番&sort=最新上市
app.MapGet("/api/hanime/search", async (
    string title,
    int? max,
    string? genre,
    string? sort,
    HanimeScraperPlaywrightClient svc) =>
{
    var list = await svc.SmartSearchAndFetchAllAsync(title, max ?? 12, sort);

    // 直接按前端插件约定返回
    return Results.Json(list);
});

// （可选）你也可以加：/api/hanime/by-url?url=... 或 /api/hanime/{id} 端点
// 这里先实现搜索入口，方便前端插件按标题搜

app.Run("http://0.0.0.0:8585");
