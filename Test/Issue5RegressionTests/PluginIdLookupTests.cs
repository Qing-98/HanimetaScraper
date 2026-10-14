using System.Collections.Concurrent;
using Jellyfin.Plugin.Hanimeta.Helpers;
using Jellyfin.Plugin.Hanimeta.Providers.DLsite;
using Jellyfin.Plugin.Hanimeta.Providers.Hanime;
using MediaBrowser.Controller.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Issue5RegressionTests;

public class PluginIdLookupTests
{
    [Theory]
    [InlineData("260626", null, false)]
    [InlineData("260626", null, true)]
    [InlineData("[260626]", null, false)]
    [InlineData("[260626]", null, true)]
    [InlineData("[260626] Example [1080p].mp4", null, false)]
    [InlineData("[260626] Example [1080p].mp4", null, true)]
    [InlineData("【260626】Example.mkv", null, false)]
    [InlineData("【260626】Example.mkv", null, true)]
    [InlineData("260626.mp4", null, false)]
    [InlineData("260626.mp4", null, true)]
    [InlineData("https://hanime1.me/watch?v=260626", null, false)]
    [InlineData("https://hanime1.me/watch?v=260626", null, true)]
    [InlineData("Example", "[260626]", false)]
    [InlineData("Example", "[260626]", true)]
    public async Task Hanime_ids_use_detail_endpoint_before_title_cleaning(string name, string? originalTitle, bool manual)
    {
        await using var backend = await FakeBackend.StartAsync();
        var provider = CreateHanimeProvider(backend);
        var info = new MovieInfo { Name = name, OriginalTitle = originalTitle };

        if (manual)
        {
            var result = Assert.Single(await provider.GetSearchResults(info, CancellationToken.None));
            Assert.Equal("260626", result.ProviderIds["Hanime"]);
            Assert.Equal("Example title", result.Name);
        }
        else
        {
            var result = await provider.GetMetadata(info, CancellationToken.None);
            Assert.True(result.HasMetadata);
            Assert.Equal("260626", result.Item.ProviderIds["Hanime"]);
            Assert.Equal("Example title", result.Item.Name);
        }

        Assert.Equal("/api/hanime/260626", Assert.Single(backend.Requests));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_provider_id_takes_precedence(bool manual)
    {
        await using var backend = await FakeBackend.StartAsync();
        var provider = CreateHanimeProvider(backend);
        var info = new MovieInfo { Name = "[260626]", ProviderIds = { ["Hanime"] = "260710" } };

        if (manual)
        {
            Assert.Equal("260710", Assert.Single(await provider.GetSearchResults(info, CancellationToken.None)).ProviderIds["Hanime"]);
        }
        else
        {
            Assert.Equal("260710", (await provider.GetMetadata(info, CancellationToken.None)).Item.ProviderIds["Hanime"]);
        }

        Assert.Equal("/api/hanime/260710", Assert.Single(backend.Requests));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bracketed_dlsite_id_uses_shared_lookup(bool manual)
    {
        await using var backend = await FakeBackend.StartAsync();
        var provider = new DLsiteUnifiedMetadataProvider(
            NullLogger<DLsiteUnifiedMetadataProvider>.Instance,
            () => new DLsiteUnifiedApiClient(NullLogger<DLsiteUnifiedApiClient>.Instance, backend.Url),
            new DLsiteMetadataMapper());
        var info = new MovieInfo { Name = "[RJ01402281] Example.mp4" };

        if (manual)
        {
            Assert.Equal("RJ01402281", Assert.Single(await provider.GetSearchResults(info, CancellationToken.None)).ProviderIds["DLsite"]);
        }
        else
        {
            var result = await provider.GetMetadata(info, CancellationToken.None);
            Assert.True(result.HasMetadata);
            Assert.Equal("RJ01402281", result.Item.ProviderIds["DLsite"]);
        }

        Assert.Equal("/api/dlsite/RJ01402281", Assert.Single(backend.Requests));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ordinary_title_keeps_title_search(bool manual)
    {
        await using var backend = await FakeBackend.StartAsync();
        var provider = CreateHanimeProvider(backend);
        var info = new MovieInfo { Name = "Example [1080p].mp4" };

        if (manual)
        {
            Assert.Empty(await provider.GetSearchResults(info, CancellationToken.None));
        }
        else
        {
            Assert.False((await provider.GetMetadata(info, CancellationToken.None)).HasMetadata);
        }

        Assert.Equal("/api/hanime/search?title=Example&max=10", Assert.Single(backend.Requests));
    }

    [Fact]
    public async Task Missing_bracketed_id_returns_no_search_result()
    {
        await using var backend = await FakeBackend.StartAsync(missing: true);
        Assert.Empty(await CreateHanimeProvider(backend).GetSearchResults(new MovieInfo { Name = "[260626]" }, CancellationToken.None));
        Assert.Equal("/api/hanime/260626", Assert.Single(backend.Requests));
    }

    [Theory]
    [InlineData("Example 2025")]
    [InlineData("Example [1080p]")]
    [InlineData("Example [123]")]
    [InlineData("RJ01402281")]
    public void Title_numbers_and_other_provider_ids_are_not_hanime_ids(string input)
    {
        Assert.False(IdHelper.TryExtractHanimeId(input, out _));
    }

    private static HanimeUnifiedMetadataProvider CreateHanimeProvider(FakeBackend backend) => new(
        NullLogger<HanimeUnifiedMetadataProvider>.Instance,
        () => new HanimeUnifiedApiClient(NullLogger<HanimeUnifiedApiClient>.Instance, backend.Url),
        new HanimeMetadataMapper());

    private sealed class FakeBackend(WebApplication app, string url, ConcurrentQueue<string> requests) : IAsyncDisposable
    {
        public string Url { get; } = url;
        public ConcurrentQueue<string> Requests { get; } = requests;

        public static async Task<FakeBackend> StartAsync(bool missing = false)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            var requests = new ConcurrentQueue<string>();
            app.MapGet("/{**path}", (HttpRequest request) =>
            {
                requests.Enqueue(request.Path + request.QueryString);
                if (request.Path.Value!.EndsWith("/search", StringComparison.Ordinal))
                {
                    return Results.Json(new { success = true, data = Array.Empty<object>() });
                }

                return missing
                    ? Results.NotFound()
                    : Results.Json(new { success = true, data = new { title = "Example title" } });
            });
            await app.StartAsync();
            var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new FakeBackend(app, url, requests);
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
