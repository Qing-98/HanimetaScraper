using System.Net;
using System.Text.Json;
using Test.NewScraperTest.Testing;

namespace Test.NewScraperTest.Suites;

internal static class MechanismSuite
{
    public static ApiTestSuite Create()
    {
        return new ApiTestSuite(
            "Mechanisms",
            new List<ApiTestCase>
            {
                ApiTestCase.Custom("Cache read-hit behavior", VerifyCacheHitAsync),
                ApiTestCase.Custom("Slot flow-control behavior", VerifySlotFlowControlAsync)
            });
    }

    private static async Task<(bool Passed, string Message)> VerifyCacheHitAsync(TestContext context, CancellationToken ct)
    {
        var beforeStats = await GetCacheStatsAsync(context, ct).ConfigureAwait(false);
        if (!beforeStats.Passed)
        {
            return (false, beforeStats.Message);
        }

        using var first = await context.HttpClient.GetAsync($"{context.BackendUrl}/api/dlsite/RJ01402281", ct).ConfigureAwait(false);
        using var second = await context.HttpClient.GetAsync($"{context.BackendUrl}/api/dlsite/RJ01402281", ct).ConfigureAwait(false);

        if (first.StatusCode != HttpStatusCode.OK || second.StatusCode != HttpStatusCode.OK)
        {
            return (false, $"detail requests failed: first={(int)first.StatusCode}, second={(int)second.StatusCode}");
        }

        var afterStats = await GetCacheStatsAsync(context, ct).ConfigureAwait(false);
        if (!afterStats.Passed)
        {
            return (false, afterStats.Message);
        }

        return afterStats.HitCount > beforeStats.HitCount
            ? (true, $"cache hits increased: {beforeStats.HitCount} -> {afterStats.HitCount}")
            : (false, $"cache hit did not increase: {beforeStats.HitCount} -> {afterStats.HitCount}");
    }

    private static async Task<(bool Passed, string Message)> VerifySlotFlowControlAsync(TestContext context, CancellationToken ct)
    {
        const int burst = 12;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, burst)
            .Select(_ => context.HttpClient.GetAsync($"{context.BackendUrl}/api/dlsite/search?title=test&max=1", ct));

        var responses = await Task.WhenAll(tasks).ConfigureAwait(false);
        sw.Stop();

        try
        {
            var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
            var busyCount = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
            var serverErrorCount = responses.Count(r => (int)r.StatusCode >= 500);

            if (serverErrorCount > 0)
            {
                return (false, $"server errors detected: {serverErrorCount}");
            }

            if (successCount == 0)
            {
                return (false, "no successful responses in burst test");
            }

            var queueObserved = sw.ElapsedMilliseconds >= 1000;
            if (busyCount > 0 || queueObserved)
            {
                return (true, $"flow-control observed: ok={successCount}, busy={busyCount}, elapsed={sw.ElapsedMilliseconds}ms");
            }

            return (false, $"flow-control signal not obvious: ok={successCount}, busy={busyCount}, elapsed={sw.ElapsedMilliseconds}ms");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private static async Task<(bool Passed, string Message, long HitCount)> GetCacheStatsAsync(TestContext context, CancellationToken ct)
    {
        using var resp = await context.HttpClient.GetAsync($"{context.BackendUrl}/cache/stats", ct).ConfigureAwait(false);
        if (resp.StatusCode != HttpStatusCode.OK)
        {
            return (false, $"cache stats request failed: {(int)resp.StatusCode}", 0L);
        }

        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("hitCount", out var hitCount) || !hitCount.TryGetInt64(out var value))
            {
                return (false, "cache stats missing hitCount", 0L);
            }

            return (true, string.Empty, value);
        }
        catch (JsonException ex)
        {
            return (false, $"invalid cache stats json: {ex.Message}", 0L);
        }
    }
}
