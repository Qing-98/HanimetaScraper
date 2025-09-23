using ScraperBackendService.Configuration;
using ScraperBackendService.Extensions;
using ScraperBackendService.Middleware;
using ScraperBackendService.Models;
using ScraperBackendService.Providers.DLsite;
using ScraperBackendService.Providers.Hanime;
using System.Text.Json;

/// <summary>
/// Main entry point for the Scraper Backend Service.
/// Configures and starts a web API server for content scraping operations.
/// </summary>
/// <example>
/// Usage examples:
///
/// // Run with default settings
/// dotnet run
///
/// // Run on custom port
/// dotnet run 9090
///
/// // Run with environment variables
/// set SCRAPER_PORT=8080
/// set SCRAPER_AUTH_TOKEN=my-secret-token
/// dotnet run
///
/// // API endpoints available:
/// GET /                           - Service info and health status
/// GET /health                     - Health check endpoint
/// GET /api/hanime/search?title=   - Search Hanime content
/// GET /api/hanime/{id}            - Get Hanime content details
/// GET /api/dlsite/search?title=   - Search DLsite content
/// GET /api/dlsite/{id}            - Get DLsite content details
/// </example>

var builder = WebApplication.CreateBuilder(args);

// Load configuration from appsettings.json and bind to ServiceConfiguration
var serviceConfig = new ServiceConfiguration();
builder.Configuration.GetSection(ServiceConfiguration.SectionName).Bind(serviceConfig);

// Override with command line arguments if provided
if (args.Length > 0 && int.TryParse(args[0], out var port))
    serviceConfig.Port = port;

// Override with environment variables if set
var envPort = Environment.GetEnvironmentVariable("SCRAPER_PORT");
if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out var ePort))
    serviceConfig.Port = ePort;

var envToken = Environment.GetEnvironmentVariable("SCRAPER_AUTH_TOKEN");
if (!string.IsNullOrEmpty(envToken))
    serviceConfig.AuthToken = envToken;

// Configure logging based on service configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (serviceConfig.EnableDetailedLogging)
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}

// Register scraping services and dependencies
builder.Services.AddScrapingServices(serviceConfig);

// Configure JSON serialization for API responses
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.WriteIndented = true;
});

var app = builder.Build();

// Add token authentication middleware
app.UseMiddleware<TokenAuthenticationMiddleware>();

// Configure request timeout middleware
app.Use(async (context, next) =>
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(serviceConfig.RequestTimeoutSeconds));
    var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
        context.RequestAborted, cts.Token).Token;

    context.RequestAborted = combinedToken;
    await next();
});

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Service information and health check endpoints
app.MapGet("/", () =>
{
    return Results.Json(ApiResponse<ServiceInfo>.Ok(new ServiceInfo
    {
        AuthEnabled = !string.IsNullOrWhiteSpace(serviceConfig.AuthToken)
    }));
});

app.MapGet("/health", () => Results.Json(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Add redirect endpoint for DLsite external links
app.MapGet("/r/dlsite/{id}", (string id) =>
{
    try
    {
        var target = ScraperBackendService.Core.Routing.IdParsers.BuildDlsiteDetailUrl(id);
        return Results.Redirect(target);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to build DLsite redirect for id={Id}", id);
        return Results.NotFound();
    }
});

// =============== Hanime API Endpoints ===============

/// <summary>
/// Search for Hanime content by title with optional filtering.
/// </summary>
/// <param name="title">Search title or keyword</param>
/// <param name="max">Maximum number of results (default: 12, max: 50)</param>
/// <param name="genre">Genre filter (optional, not currently implemented)</param>
/// <param name="sort">Sort order (optional, not currently implemented)</param>
/// <param name="provider">Hanime provider instance (injected)</param>
/// <param name="ct">Cancellation token</param>
/// <returns>List of Hanime metadata objects</returns>
/// <example>
/// GET /api/hanime/search?title=Love&max=5
/// GET /api/hanime/search?title=Romance&max=10&genre=Comedy
/// </example>
app.MapGet("/api/hanime/search", async (
    string title,
    int? max,
    string? genre,
    string? sort,
    HanimeProvider provider,
    CancellationToken ct) =>
{
    try
    {
        logger.LogInformation("Hanime search request: {Title}", title);

        var maxResults = Math.Min(max ?? 12, 50); // Limit maximum results to prevent overload
        var hits = await provider.SearchAsync(title, maxResults, ct);

        var results = new List<HanimeMetadata>();
        var semaphore = new SemaphoreSlim(serviceConfig.MaxConcurrentRequests, serviceConfig.MaxConcurrentRequests);

        // Process search hits concurrently with rate limiting
        var tasks = hits.Take(maxResults).Select(async hit =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var detail = await provider.FetchDetailAsync(hit.DetailUrl, ct);
                if (detail != null)
                {
                    lock (results)
                    {
                        results.Add(detail);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch detail for {Url}", hit.DetailUrl);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        if (results.Count > 0)
        {
            logger.LogInformation("Hanime search completed: {Title}, found {Count} results", title, results.Count);
        }
        else
        {
            logger.LogInformation("Hanime search completed: {Title}, no results found", title);
        }
        return Results.Json(ApiResponse<List<HanimeMetadata>>.Ok(results));
    }
    catch (OperationCanceledException)
    {
        logger.LogWarning("Hanime search request cancelled: {Title}", title);
        return Results.Json(ApiResponse<List<HanimeMetadata>>.Fail("Request cancelled"));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Hanime search error: {Title}", title);
        return Results.Json(ApiResponse<List<HanimeMetadata>>.Fail($"Search error: {ex.Message}"));
    }
});

/// <summary>
/// Get detailed information for a specific Hanime content by ID.
/// </summary>
/// <param name="id">Hanime content ID (e.g., "12345")</param>
/// <param name="provider">Hanime provider instance (injected)</param>
/// <param name="ct">Cancellation token</param>
/// <returns>Detailed Hanime metadata or error response</returns>
/// <example>
/// GET /api/hanime/12345
/// GET /api/hanime/86994
/// </example>
app.MapGet("/api/hanime/{id}", async (
    string id,
    HanimeProvider provider,
    CancellationToken ct) =>
{
    try
    {
        logger.LogInformation("Hanime detail request: {Id}", id);

        if (!provider.TryParseId(id, out var parsedId))
        {
            logger.LogWarning("Hanime invalid ID format: {Id}", id);
            return Results.Json(ApiResponse<HanimeMetadata>.Fail($"Invalid Hanime ID: {id}"));
        }

        var detailUrl = provider.BuildDetailUrlById(parsedId);
        var result = await provider.FetchDetailAsync(detailUrl, ct);

        if (result != null)
        {
            logger.LogInformation("Hanime detail found: {Id}", id);
            return Results.Json(ApiResponse<HanimeMetadata>.Ok(result));
        }
        else
        {
            logger.LogWarning("Hanime detail not found: {Id}", id);
            return Results.Json(ApiResponse<HanimeMetadata>.Fail($"Content not found: {id}"));
        }
    }
    catch (OperationCanceledException)
    {
        logger.LogWarning("Hanime detail request cancelled: {Id}", id);
        return Results.Json(ApiResponse<HanimeMetadata>.Fail("Request cancelled"));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Hanime detail error: {Id}", id);
        return Results.Json(ApiResponse<HanimeMetadata>.Fail($"Detail error: {ex.Message}"));
    }
});

// =============== DLsite API Endpoints ===============

/// <summary>
/// Search for DLsite content by title.
/// </summary>
/// <param name="title">Search title or keyword (supports Japanese text)</param>
/// <param name="max">Maximum number of results (default: 12, max: 50)</param>
/// <param name="provider">DLsite provider instance (injected)</param>
/// <param name="ct">Cancellation token</param>
/// <returns>List of DLsite metadata objects</returns>
/// <example>
/// GET /api/dlsite/search?title=恋爱&max=5
/// GET /api/dlsite/search?title=RJ123456&max=1
/// </example>
app.MapGet("/api/dlsite/search", async (
    string title,
    int? max,
    DlsiteProvider provider,
    CancellationToken ct) =>
{
    try
    {
        logger.LogInformation("DLsite search request: {Title}", title);

        var maxResults = Math.Min(max ?? 12, 50);
        var hits = await provider.SearchAsync(title, maxResults, ct);

        var results = new List<HanimeMetadata>();
        var semaphore = new SemaphoreSlim(serviceConfig.MaxConcurrentRequests, serviceConfig.MaxConcurrentRequests);

        // Process search hits concurrently with rate limiting
        var tasks = hits.Take(maxResults).Select(async hit =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var detail = await provider.FetchDetailAsync(hit.DetailUrl, ct);
                if (detail != null)
                {
                    lock (results)
                    {
                        results.Add(detail);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch detail for {Url}", hit.DetailUrl);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        if (results.Count > 0)
        {
            logger.LogInformation("DLsite search completed: {Title}, found {Count} results", title, results.Count);
        }
        else
        {
            logger.LogInformation("DLsite search completed: {Title}, no results found", title);
        }
        return Results.Json(ApiResponse<List<HanimeMetadata>>.Ok(results));
    }
    catch (OperationCanceledException)
    {
        logger.LogWarning("DLsite search request cancelled: {Title}", title);
        return Results.Json(ApiResponse<List<HanimeMetadata>>.Fail("Request cancelled"));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "DLsite search error: {Title}", title);
        return Results.Json(ApiResponse<List<HanimeMetadata>>.Fail($"Search error: {ex.Message}"));
    }
});

/// <summary>
/// Get detailed information for a specific DLsite product by ID.
/// </summary>
/// <param name="id">DLsite product ID (e.g., "RJ123456", "VJ012345")</param>
/// <param name="provider">DLsite provider instance (injected)</param>
/// <param name="ct">Cancellation token</param>
/// <returns>Detailed DLsite metadata or error response</returns>
/// <example>
/// GET /api/dlsite/RJ123456
/// GET /api/dlsite/RJ01402281
/// </example>
app.MapGet("/api/dlsite/{id}", async (
    string id,
    DlsiteProvider provider,
    CancellationToken ct) =>
{
    try
    {
        logger.LogInformation("DLsite detail request: {Id}", id);

        if (!provider.TryParseId(id, out var parsedId))
        {
            logger.LogWarning("DLsite invalid ID format: {Id}", id);
            return Results.Json(ApiResponse<HanimeMetadata>.Fail($"Invalid DLsite ID: {id}"));
        }

        var detailUrl = provider.BuildDetailUrlById(parsedId);
        var result = await provider.FetchDetailAsync(detailUrl, ct);

        if (result != null)
        {
            logger.LogInformation("DLsite detail found: {Id}", id);
            return Results.Json(ApiResponse<HanimeMetadata>.Ok(result));
        }
        else
        {
            logger.LogWarning("DLsite detail not found: {Id}", id);
            return Results.Json(ApiResponse<HanimeMetadata>.Fail($"Content not found: {id}"));
        }
    }
    catch (OperationCanceledException)
    {
        logger.LogWarning("DLsite detail request cancelled: {Id}", id);
        return Results.Json(ApiResponse<HanimeMetadata>.Fail("Request cancelled"));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "DLsite detail error: {Id}", id);
        return Results.Json(ApiResponse<HanimeMetadata>.Fail($"Detail error: {ex.Message}"));
    }
});

// Build and start the service
var listenUrl = $"http://{serviceConfig.Host}:{serviceConfig.Port}";

logger.LogInformation("Starting ScraperBackendService on {Url}", listenUrl);
logger.LogInformation("Authentication: {Status}",
    string.IsNullOrWhiteSpace(serviceConfig.AuthToken) ? "Disabled" : "Enabled");

try
{
    app.Run(listenUrl);
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Failed to start service on {Url}", listenUrl);
    throw;
}
