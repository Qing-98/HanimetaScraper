using System.Net.Http;

namespace Test.NewScraperTest.Testing;

internal sealed record TestContext(
    string BackendUrl,
    HttpClient HttpClient,
    string? ApiToken,
    bool AuthEnabled);
