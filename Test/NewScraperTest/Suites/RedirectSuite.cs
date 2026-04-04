using System.Net;
using Test.NewScraperTest.Testing;

namespace Test.NewScraperTest.Suites;

internal static class RedirectSuite
{
    public static ApiTestSuite Create()
    {
        return new ApiTestSuite(
            "Redirect",
            new List<ApiTestCase>
            {
                ApiTestCase.Get(
                    "DLsite redirect",
                    "/r/dlsite/RJ01402281",
                    new[] { HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently, HttpStatusCode.Found }),

                ApiTestCase.Get(
                    "DLsite redirect invalid id",
                    "/r/dlsite/invalid-id",
                    new[] { HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently, HttpStatusCode.Found })
            });
    }
}
