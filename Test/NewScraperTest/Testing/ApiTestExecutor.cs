using System.Diagnostics;
using System.Net;

namespace Test.NewScraperTest.Testing;

internal static class ApiTestExecutor
{
    public static async Task<ApiTestResult> ExecuteAsync(TestContext context, string suiteName, ApiTestCase testCase, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (testCase.CustomCheck is not null)
        {
            var (passed, message) = await testCase.CustomCheck(context, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            return new ApiTestResult(
                suiteName,
                testCase.Name,
                passed,
                HttpStatusCode.OK,
                sw.ElapsedMilliseconds,
                message);
        }

        var url = $"{context.BackendUrl}{testCase.Endpoint}";

        try
        {
            using var request = new HttpRequestMessage(testCase.Method, url);
            using var response = await context.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (!testCase.ExpectedStatuses.Contains(response.StatusCode))
            {
                return new ApiTestResult(
                    suiteName,
                    testCase.Name,
                    false,
                    response.StatusCode,
                    sw.ElapsedMilliseconds,
                    $"expected [{string.Join(",", testCase.ExpectedStatuses.Select(s => ((int)s).ToString()))}], actual {(int)response.StatusCode}");
            }

            if (testCase.BodyValidator is null)
            {
                return new ApiTestResult(suiteName, testCase.Name, true, response.StatusCode, sw.ElapsedMilliseconds, string.Empty);
            }

            var (passed, message) = testCase.BodyValidator(body);
            return new ApiTestResult(suiteName, testCase.Name, passed, response.StatusCode, sw.ElapsedMilliseconds, message);
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return new ApiTestResult(suiteName, testCase.Name, false, 0, sw.ElapsedMilliseconds, "request timeout");
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new ApiTestResult(suiteName, testCase.Name, false, 0, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
