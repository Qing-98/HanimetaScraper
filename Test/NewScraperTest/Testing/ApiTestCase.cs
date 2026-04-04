using System.Net;

namespace Test.NewScraperTest.Testing;

internal sealed record ApiTestCase(
    string Name,
    string Endpoint,
    HttpMethod Method,
    IReadOnlyCollection<HttpStatusCode> ExpectedStatuses,
    Func<string, (bool Passed, string Message)>? BodyValidator = null,
    Func<TestContext, CancellationToken, Task<(bool Passed, string Message)>>? CustomCheck = null)
{
    public static ApiTestCase Get(
        string name,
        string endpoint,
        HttpStatusCode expectedStatus,
        Func<string, (bool Passed, string Message)>? validator = null)
        => new(name, endpoint, HttpMethod.Get, new[] { expectedStatus }, validator);

    public static ApiTestCase Get(
        string name,
        string endpoint,
        IEnumerable<HttpStatusCode> expectedStatuses,
        Func<string, (bool Passed, string Message)>? validator = null)
        => new(name, endpoint, HttpMethod.Get, expectedStatuses.ToArray(), validator);

    public static ApiTestCase Delete(
        string name,
        string endpoint,
        HttpStatusCode expectedStatus,
        Func<string, (bool Passed, string Message)>? validator = null)
        => new(name, endpoint, HttpMethod.Delete, new[] { expectedStatus }, validator);

    public static ApiTestCase Custom(
        string name,
        Func<TestContext, CancellationToken, Task<(bool Passed, string Message)>> customCheck)
    {
        ArgumentNullException.ThrowIfNull(customCheck);
        return new ApiTestCase(
            name,
            Endpoint: string.Empty,
            Method: HttpMethod.Get,
            ExpectedStatuses: Array.Empty<HttpStatusCode>(),
            BodyValidator: null,
            CustomCheck: customCheck);
    }
}
