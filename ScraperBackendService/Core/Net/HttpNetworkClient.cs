using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ScraperBackendService.Core.Abstractions;

namespace ScraperBackendService.Core.Net;

public sealed class HttpNetworkClient : INetworkClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpNetworkClient> _logger;

    // Reuse SocketsHttpHandler's high-performance defaults
    public HttpNetworkClient(ILogger<HttpNetworkClient> logger, HttpClient? httpClient = null)
    {
        _logger = logger;
        _http = httpClient ?? CreateDefaultClient();
    }

    public async Task<string> GetHtmlAsync(string url, CancellationToken ct)
    {
        _logger.LogDebug("Getting HTML from: {Url}", url);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public async Task<JsonDocument> GetJsonAsync(string url, IDictionary<string, string>? headers, CancellationToken ct)
    {
        _logger.LogDebug("Getting JSON from: {Url}", url);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (headers != null)
        {
            foreach (var kv in headers)
            {
                // Some common headers are in strongly-typed collections and need to be routed
                if (string.Equals(kv.Key, "Referer", StringComparison.OrdinalIgnoreCase))
                    req.Headers.Referrer = new Uri(kv.Value);
                else
                    req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(s, cancellationToken: ct);
    }

    // HTTP version does not provide Page
    public Task<Microsoft.Playwright.IPage?> OpenPageAsync(string url, CancellationToken ct)
    {
        _logger.LogDebug("OpenPageAsync called on HttpNetworkClient - returning null (use PlaywrightNetworkClient for page support)");
        return Task.FromResult<Microsoft.Playwright.IPage?>(null);
    }

    public void Dispose() => _http.Dispose();

    private static HttpClient CreateDefaultClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(90),
            MaxConnectionsPerServer = 12,
            EnableMultipleHttp2Connections = true,
            UseCookies = false
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118 Safari/537.36");
        http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9,ja;q=0.8,zh-CN;q=0.7");
        return http;
    }
}
