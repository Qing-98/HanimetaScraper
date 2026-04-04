using System.Net;
using Test.NewScraperTest.Testing;

namespace Test.NewScraperTest.Suites;

internal static class CacheSuite
{
    public static ApiTestSuite Create()
    {
        return new ApiTestSuite(
            "Cache",
            new List<ApiTestCase>
            {
                ApiTestCase.Get("Cache stats", "/cache/stats", HttpStatusCode.OK, ApiResponseAssertions.HasCacheStatsShape),
                ApiTestCase.Delete("Cache clear", "/cache/clear", HttpStatusCode.OK, ApiResponseAssertions.IsJsonObject),
                ApiTestCase.Delete("Cache remove entry", "/cache/dlsite/RJ01402281", HttpStatusCode.OK, ApiResponseAssertions.IsJsonObject)
            });
    }
}
