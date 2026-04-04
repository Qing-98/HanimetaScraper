using System.Text.Json;

namespace Test.NewScraperTest.Testing;

internal static class ApiResponseAssertions
{
    public static (bool Passed, string Message) IsJsonObject(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? (true, string.Empty)
                : (false, "response is not a JSON object");
        }
        catch (JsonException ex)
        {
            return (false, $"invalid JSON: {ex.Message}");
        }
    }

    public static (bool Passed, string Message) HasSuccessTrue(string body)
    {
        var parsed = ParseRoot(body);
        if (!parsed.Passed)
        {
            return parsed;
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True
            ? (true, string.Empty)
            : (false, "expected success=true");
    }

    public static (bool Passed, string Message) HasSuccessFalse(string body)
    {
        var parsed = ParseRoot(body);
        if (!parsed.Passed)
        {
            return parsed;
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False
            ? (true, string.Empty)
            : (false, "expected success=false");
    }

    public static (bool Passed, string Message) HasDataArray(string body)
    {
        var parsed = ParseRoot(body);
        if (!parsed.Passed)
        {
            return parsed;
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
            ? (true, string.Empty)
            : (false, "expected data array");
    }

    public static (bool Passed, string Message) HasDataObjectId(string body)
    {
        var parsed = ParseRoot(body);
        if (!parsed.Passed)
        {
            return parsed;
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return (false, "expected data object");
        }

        if (!data.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
        {
            return (false, "expected non-empty data.id");
        }

        return (true, string.Empty);
    }

    public static (bool Passed, string Message) HasAuthEnabledFlag(string body)
    {
        var parsed = ParseRoot(body);
        if (!parsed.Passed)
        {
            return parsed;
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return (false, "expected data object");
        }

        return data.TryGetProperty("authEnabled", out var authEnabled)
               && authEnabled.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? (true, string.Empty)
            : (false, "expected data.authEnabled bool");
    }

    public static (bool Passed, string Message) IsHealthyStatus(string body)
    {
        var parsed = ParseRoot(body);
        if (!parsed.Passed)
        {
            return parsed;
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("status", out var status)
               && status.ValueKind == JsonValueKind.String
               && string.Equals(status.GetString(), "healthy", StringComparison.OrdinalIgnoreCase)
            ? (true, string.Empty)
            : (false, "expected status=healthy");
    }

    public static (bool Passed, string Message) HasCacheStatsShape(string body)
    {
        var parsed = ParseRoot(body);
        if (!parsed.Passed)
        {
            return parsed;
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        return root.TryGetProperty("hitCount", out _)
               && root.TryGetProperty("missCount", out _)
               && root.TryGetProperty("evictionCount", out _)
               && root.TryGetProperty("hitRatio", out _)
            ? (true, string.Empty)
            : (false, "cache stats fields missing");
    }

    private static (bool Passed, string Message) ParseRoot(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? (true, string.Empty)
                : (false, "response is not JSON object");
        }
        catch (JsonException ex)
        {
            return (false, $"invalid JSON: {ex.Message}");
        }
    }
}
