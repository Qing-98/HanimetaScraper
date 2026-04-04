using System.Net;

namespace Test.NewScraperTest.Testing;

internal sealed record ApiTestResult(
    string Suite,
    string Name,
    bool Passed,
    HttpStatusCode StatusCode,
    long ElapsedMilliseconds,
    string Message);
