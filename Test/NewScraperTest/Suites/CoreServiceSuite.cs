using System.Net;
using Test.NewScraperTest.Testing;

namespace Test.NewScraperTest.Suites;

internal static class CoreServiceSuite
{
    public static ApiTestSuite Create()
    {
        return new ApiTestSuite(
            "Core Service",
            new List<ApiTestCase>
            {
                ApiTestCase.Get("Service Info", "/", HttpStatusCode.OK, ApiResponseAssertions.HasAuthEnabledFlag),
                ApiTestCase.Get("Health", "/health", HttpStatusCode.OK, ApiResponseAssertions.IsHealthyStatus)
            });
    }
}
