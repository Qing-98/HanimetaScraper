using System.Diagnostics;

namespace Test.NewScraperTest.Testing;

internal static class TestSuiteRunner
{
    public static async Task<IReadOnlyCollection<ApiTestResult>> RunSuiteAsync(TestContext context, ApiTestSuite suite)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {suite.Name} ===");

        var results = new List<ApiTestResult>(suite.Cases.Count);
        foreach (var testCase in suite.Cases)
        {
            var result = await ApiTestExecutor.ExecuteAsync(context, suite.Name, testCase).ConfigureAwait(false);
            results.Add(result);
            PrintResult(result);
        }

        PrintSummary(suite.Name, results, null);
        return results;
    }

    public static async Task<IReadOnlyCollection<ApiTestResult>> RunSuiteParallelAsync(TestContext context, ApiTestSuite suite)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {suite.Name} (parallel) ===");

        var sw = Stopwatch.StartNew();
        var tasks = suite.Cases.Select(testCase => ApiTestExecutor.ExecuteAsync(context, suite.Name, testCase));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        sw.Stop();

        foreach (var result in results)
        {
            PrintResult(result);
        }

        PrintSummary(suite.Name, results, sw.Elapsed.TotalSeconds);
        return results;
    }

    public static void PrintOverallSummary(IEnumerable<ApiTestResult> results)
    {
        var list = results.ToList();
        var passed = list.Count(r => r.Passed);
        var failed = list.Count - passed;

        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine($"Overall: {passed}/{list.Count} passed, {failed} failed");
        if (failed > 0)
        {
            Console.WriteLine("Failed cases:");
            foreach (var failedCase in list.Where(r => !r.Passed))
            {
                Console.WriteLine($"- [{failedCase.Suite}] {failedCase.Name}: {failedCase.Message}");
            }
        }
        Console.WriteLine("==============================================");
    }

    private static void PrintResult(ApiTestResult result)
    {
        var marker = result.Passed ? "PASS" : "FAIL";
        Console.WriteLine($"[{marker}] {result.Name} | {(int)result.StatusCode} {result.StatusCode} | {result.ElapsedMilliseconds}ms");
        if (!result.Passed && !string.IsNullOrWhiteSpace(result.Message))
        {
            Console.WriteLine($"       -> {result.Message}");
        }
    }

    private static void PrintSummary(string suiteName, IReadOnlyCollection<ApiTestResult> results, double? totalSeconds)
    {
        var passed = results.Count(r => r.Passed);
        var failed = results.Count - passed;
        var avg = results.Count == 0 ? 0 : results.Average(r => r.ElapsedMilliseconds);

        Console.WriteLine($"Summary [{suiteName}]: {passed}/{results.Count} passed, {failed} failed, avg {avg:F0}ms");
        if (totalSeconds.HasValue)
        {
            Console.WriteLine($"Elapsed: {totalSeconds.Value:F2}s, throughput: {(results.Count / totalSeconds.Value):F1} req/s");
        }
    }
}
