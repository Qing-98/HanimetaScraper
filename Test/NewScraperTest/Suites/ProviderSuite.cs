using System.Net;
using Test.NewScraperTest.Testing;

namespace Test.NewScraperTest.Suites;

internal static class ProviderSuite
{
    public static ApiTestSuite CreateDlsiteSuite()
    {
        return new ApiTestSuite(
            "DLsite",
            new List<ApiTestCase>
            {
                ApiTestCase.Get(
                    "DLsite search",
                    "/api/dlsite/search?title=恋爱&max=2",
                    HttpStatusCode.OK,
                    body => Combine(ApiResponseAssertions.HasSuccessTrue(body), ApiResponseAssertions.HasDataArray(body))),

                ApiTestCase.Get(
                    "DLsite detail",
                    "/api/dlsite/RJ01402281",
                    HttpStatusCode.OK,
                    body => Combine(ApiResponseAssertions.HasSuccessTrue(body), ApiResponseAssertions.HasDataObjectId(body))),

                ApiTestCase.Get(
                    "DLsite invalid id",
                    "/api/dlsite/invalid-id",
                    HttpStatusCode.BadRequest,
                    ApiResponseAssertions.HasSuccessFalse)
            });
    }

    public static ApiTestSuite CreateHanimeSuite()
    {
        return new ApiTestSuite(
            "Hanime",
            new List<ApiTestCase>
            {
                ApiTestCase.Get(
                    "Hanime search",
                    "/api/hanime/search?title=Love&max=2",
                    HttpStatusCode.OK,
                    body => Combine(ApiResponseAssertions.HasSuccessTrue(body), ApiResponseAssertions.HasDataArray(body))),

                ApiTestCase.Get(
                    "Hanime detail",
                    "/api/hanime/86994",
                    HttpStatusCode.OK,
                    body => Combine(ApiResponseAssertions.HasSuccessTrue(body), ApiResponseAssertions.HasDataObjectId(body))),

                ApiTestCase.Get(
                    "Hanime invalid id",
                    "/api/hanime/invalid-id",
                    HttpStatusCode.BadRequest,
                    ApiResponseAssertions.HasSuccessFalse)
            });
    }

    private static (bool Passed, string Message) Combine(
        (bool Passed, string Message) first,
        (bool Passed, string Message) second)
    {
        if (!first.Passed)
        {
            return first;
        }

        return second;
    }
}
