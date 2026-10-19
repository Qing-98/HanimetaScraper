using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using ScraperBackendService.Browser;
using ScraperBackendService.Core.Abstractions;
using ScraperBackendService.Providers.Hanime;
using Xunit;

namespace Issue5RegressionTests;

public class HanimeFailureTests
{
    private const string Url = "https://hanime1.me/watch?v=260612";
    private const string BlockedHtml = "<html><head><title>Attention Required! | Cloudflare</title></head><body>Sorry, you have been blocked</body></html>";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Blocked_pages_are_errors_instead_of_missing_content(bool search)
    {
        var provider = CreateProvider(new StubNetworkClient(BlockedHtml));
        var error = search
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SearchAsync("Example", 10, CancellationToken.None))
            : await Assert.ThrowsAsync<InvalidOperationException>(() => provider.FetchDetailAsync(Url, CancellationToken.None));
        Assert.Contains("verification or blocked page", error.Message);
    }

    [Fact]
    public async Task Network_failure_is_not_converted_to_null()
    {
        var failure = new HttpRequestException("Connection failed");
        var provider = CreateProvider(new StubNetworkClient("", failure));
        Assert.Same(failure, await Assert.ThrowsAsync<HttpRequestException>(() => provider.FetchDetailAsync(Url, CancellationToken.None)));
    }

    [Fact]
    public async Task Cancellation_is_not_converted_to_null()
    {
        var provider = CreateProvider(new StubNetworkClient("", new OperationCanceledException()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.FetchDetailAsync(Url, CancellationToken.None));
    }

    [Fact]
    public async Task Failed_timeout_fallback_is_not_converted_to_empty_search()
    {
        var provider = CreateProvider(new StubNetworkClient("", new TimeoutException(), new HttpRequestException("Connection failed")));
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.SearchAsync("Example", 10, CancellationToken.None));
    }

    [Fact]
    public async Task Valid_detail_html_still_returns_metadata()
    {
        var provider = CreateProvider(new StubNetworkClient("<h3 id='shareBtn-title'>Example title</h3><video poster='https://example.com/cover.jpg'></video>"));
        var metadata = await provider.FetchDetailAsync(Url, CancellationToken.None);
        Assert.NotNull(metadata);
        Assert.Equal("260612", metadata.ID);
        Assert.Equal("Example title", metadata.Title);
        Assert.Equal("https://example.com/cover.jpg", metadata.Primary);
    }

    [Fact]
    public async Task Non_challenge_missing_content_still_returns_null()
    {
        var provider = CreateProvider(new StubNetworkClient("<html><body>Content not found</body></html>"));
        Assert.Null(await provider.FetchDetailAsync(Url, CancellationToken.None));
    }

    private static HanimeProvider CreateProvider(INetworkClient client) => new(
        client, NullLogger<HanimeProvider>.Instance, new ChallengeDetector(NullLogger.Instance));

    private sealed class StubNetworkClient(string html, Exception? pageError = null, Exception? htmlError = null) : INetworkClient
    {
        public Task<IPage?> OpenPageAsync(string url, CancellationToken ct) => pageError is null
            ? Task.FromResult<IPage?>(null)
            : Task.FromException<IPage?>(pageError);

        public Task<string> GetHtmlAsync(string url, CancellationToken ct) => htmlError is null
            ? Task.FromResult(html)
            : Task.FromException<string>(htmlError);

        public Task<JsonDocument> GetJsonAsync(string url, IDictionary<string, string>? headers, CancellationToken ct) => throw new NotSupportedException();
    }
}
