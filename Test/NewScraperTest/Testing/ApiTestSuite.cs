namespace Test.NewScraperTest.Testing;

internal sealed record ApiTestSuite(string Name, IReadOnlyCollection<ApiTestCase> Cases);
