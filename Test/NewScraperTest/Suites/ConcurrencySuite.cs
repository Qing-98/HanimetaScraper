using System.Net;
using Test.NewScraperTest.Testing;

namespace Test.NewScraperTest.Suites;

internal static class ConcurrencySuite
{
    public static ApiTestSuite Create(int eachProvider)
    {
        if (eachProvider <= 0)
        {
            eachProvider = 5;
        }

        var cases = new List<ApiTestCase>(eachProvider * 2);
        for (var i = 0; i < eachProvider; i++)
        {
            cases.Add(ApiTestCase.Get(
                $"Hanime concurrent #{i + 1}",
                "/api/hanime/search?title=test&max=1",
                HttpStatusCode.OK,
                ApiResponseAssertions.HasSuccessTrue));

            cases.Add(ApiTestCase.Get(
                $"DLsite concurrent #{i + 1}",
                "/api/dlsite/search?title=test&max=1",
                HttpStatusCode.OK,
                ApiResponseAssertions.HasSuccessTrue));
        }

        return new ApiTestSuite("Concurrency", cases);
    }
}
