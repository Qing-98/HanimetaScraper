using Microsoft.Playwright;
using ScraperBackendService.Models;
using ScraperBackendService.Services;

var builder = WebApplication.CreateBuilder(args);

// 1) ȷ����װ��������״����л����أ�
//await Playwright.InstallAsync();
var playwright = await Playwright.CreateAsync();
var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = true
});

// 2) ���� Playwright/Browser����������
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

// 3) ע�� Hanime ���������������� Browser��
builder.Services.AddScoped<HanimeScraperPlaywrightClient>(sp =>
{
    var browser = sp.GetRequiredService<Task<IBrowser>>().GetAwaiter().GetResult();
    var logger = sp.GetRequiredService<ILogger<HanimeScraperPlaywrightClient>>();
    return new HanimeScraperPlaywrightClient(browser, logger);
});

builder.Services.AddLogging();

var app = builder.Build();

// �������
app.MapGet("/", () => Results.Ok(new { ok = true, service = "ScraperBackendService", ts = DateTime.UtcNow }));

// =============== Hanime ===============
// ������/api/hanime/search?title=xxx&max=12&genre=�Y��&sort=��������
app.MapGet("/api/hanime/search", async (
    string title,
    int? max,
    string? genre,
    string? sort,
    HanimeScraperPlaywrightClient svc) =>
{
    var list = await svc.SmartSearchAndFetchAllAsync(title, max ?? 12, sort);

    // ֱ�Ӱ�ǰ�˲��Լ������
    return Results.Json(list);
});

// ����ѡ����Ҳ���Լӣ�/api/hanime/by-url?url=... �� /api/hanime/{id} �˵�
// ������ʵ��������ڣ�����ǰ�˲����������

app.Run("http://0.0.0.0:8585");
