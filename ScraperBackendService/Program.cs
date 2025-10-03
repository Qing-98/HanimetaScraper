using ScraperBackendService.Configuration;
using ScraperBackendService.Extensions;
using ScraperBackendService.Middleware;
using ScraperBackendService.Models;
using ScraperBackendService.Providers.DLsite;
using ScraperBackendService.Providers.Hanime;
using ScraperBackendService.Core.Logging;
using ScraperBackendService.Core.Caching;
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
/// GET /api/hanime/search?title=   - Search Hanime content by title (no ID detection)
/// GET /api/hanime/{id}            - Get Hanime content details by specific ID
/// GET /api/dlsite/search?title=   - Search DLsite content by title (no ID detection)
/// GET /api/dlsite/{id}            - Get DLsite content details by specific ID
/// 
/// Note: Search endpoints now strictly treat input as search keywords.
/// Even if the input looks like anID (e.g., "123456" or "RJ123456"), 
/// it will be used as a search term rather than switching to ID-based retrieval.
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

// Configure precise logging levels to reduce noise
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Extensions.Hosting", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Error);
builder.Logging.AddFilter("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogLevel.Error);
builder.Logging.AddFilter("Microsoft.AspNetCore.StaticFiles", LogLevel.Error);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);

// Set application logging level to Information by default
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("ScraperBackendService", LogLevel.Information);

// Register scraping services and dependencies
builder.Services.AddScrapingServices(serviceConfig);

// Register metadata cache as singleton
builder.Services.AddSingleton<MetadataCache>();

// Register Playwright cleanup hosted service
builder.Services.AddHostedService<PlaywrightCleanupService>();

// Configure JSON serialization for API responses
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.WriteIndented = true;
});

var app = builder.Build();

// Add token authentication middleware
app.UseMiddleware<TokenAuthenticationMiddleware>();

// Add memory optimization middleware to prevent memory bloat
app.UseMiddleware<MemoryOptimizationMiddleware>();

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

// Global exception handler middleware - logs unhandled exceptions from request pipeline
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
        if (ex != null)
        {
            logger.LogError(ex, "Unhandled exception in request pipeline");
        }
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Internal server error");
    });
});

// Subscribe to process-level and task-level unobserved exceptions to ensure they are logged
AppDomain.CurrentDomain.UnhandledException += (s, e) =>
{
    if (e.ExceptionObject is Exception ex)
    {
        logger.LogError(ex, "Unhandled domain exception");
    }
    else
    {
        logger.LogError("Unhandled domain exception: {Obj}", e.ExceptionObject?.ToString() ?? "<null>");
    }
};

TaskScheduler.UnobservedTaskException += (s, e) =>
{
    logger.LogError(e.Exception, "Unobserved task exception");
    e.SetObserved();
};

// Service information and health check endpoints
app.MapGet("/", () =>
{
    return Results.Json(ApiResponse<ServiceInfo>.Ok(new ServiceInfo
    {
        AuthEnabled = !string.IsNullOrWhiteSpace(serviceConfig.AuthToken)
    }));
});

app.MapGet("/health", () => Results.Json(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Cache statistics endpoint for monitoring cache performance
app.MapGet("/cache/stats", (MetadataCache cache) =>
{
    var stats = cache.GetStatistics();
    return Results.Json(new
    {
        hitCount = stats.HitCount,
        missCount = stats.MissCount,
        evictionCount = stats.EvictionCount,
        totalRequests = stats.TotalRequests,
        hitRatio = $"{stats.HitRatio:P2}",
        timestamp = DateTime.UtcNow
    });
});

// Cache management endpoints
app.MapDelete("/cache/clear", (MetadataCache cache) =>
{
    cache.Clear();
    return Results.Json(new { message = "Cache cleared successfully", timestamp = DateTime.UtcNow });
});

app.MapDelete("/cache/{provider}/{id}", (string provider, string id, MetadataCache cache) =>
{
    cache.Remove(provider, id);
    return Results.Json(new { message = $"Cache entry removed for {provider}:{id}", timestamp = DateTime.UtcNow });
});

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
/// GET /api/hanime/search?title=123456&max=5 (will search for "123456" as text, not as ID)
/// </example>
app.MapGet("/api/hanime/search", async (
    string title,
    int? max,
    string? genre,
    string? sort,
    HanimeProvider provider,
    ScraperBackendService.Core.Concurrency.HanimeConcurrencyLimiter hanimeLimiter,
    CancellationToken ct) =>
{
    var acquired = false;
    
    try
    {
        acquired = await hanimeLimiter.TryWaitAsync(0, ct).ConfigureAwait(false);
        if (!acquired)
        {
            logger.LogRateLimit("HanimeSearch");
            return Results.Json(ApiResponse<List<Metadata>>.Fail("Service busy. Retry later."), statusCode: 429);
        }

        // Always log search initiation - not affected by log level
        logger.LogAlways("HanimeSearch", $"Title search started: '{title}'");

        var maxResults = Math.Min(max ?? 12, 50);
        var hits = await provider.SearchAsync(title, maxResults, ct);

        var results = new List<Metadata>();
        
        // Remove internal concurrency control - rely on provider limits only
        var tasks = hits.Take(maxResults).Select(async hit =>
        {
            try
            {
                var detail = await provider.FetchDetailAsync(hit.DetailUrl, ct).ConfigureAwait(false);
                if (detail != null)
                {
                    lock (results)
                    {
                        results.Add(detail);
                    }
                }
            }
            catch (Exception)
            {
                logger.LogWarningLite("DetailFetch", "Failed", hit.DetailUrl);
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Always log search completion - not affected by log level
        logger.LogAlways("HanimeSearch", $"Title search completed: '{title}' -> {results.Count} results");
        
        return Results.Json(ApiResponse<List<Metadata>>.Ok(results));
    }
    catch (OperationCanceledException)
    {
        logger.LogWarningLite("HanimeSearch", "Cancelled", title);
        return Results.Json(ApiResponse<List<Metadata>>.Fail("Request cancelled"));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[HanimeSearch] ❌ {Title}", title);
        return Results.Json(ApiResponse<List<Metadata>>.Fail($"Search error: {ex.Message}"));
    }
    finally
    {
        if (acquired)
        {
            try { hanimeLimiter.Release(); } catch { }
        }
    }
});

/// <summary>
/// Get detailed information for a specific Hanime content by ID.
/// </summary>
/// <param name="id">Hanime content ID (e.g., "12345")</param>
/// <param name="provider">Hanime provider instance (injected)</param>
/// <param name="hanimeLimiter">Hanime concurrency limiter (shared with search)</param>
/// <param name="cache">Metadata cache for preventing duplicate requests</param>
/// <param name="ct">Cancellation token</param>
/// <returns>Detailed Hanime metadata or error response</returns>
/// <example>
/// GET /api/hanime/12345
/// GET /api/hanime/86994
/// </example>
app.MapGet("/api/hanime/{id}", async (
    string id,
    HanimeProvider provider,
    ScraperBackendService.Core.Concurrency.HanimeConcurrencyLimiter hanimeLimiter,
    MetadataCache cache,
    CancellationToken ct) =>
{
    try
    {
        // Always log ID query initiation - not affected by log level
        logger.LogAlways("HanimeDetail", $"ID query started: '{id}'");

        if (!provider.TryParseId(id, out var parsedId))
        {
            logger.LogWarningLite("HanimeDetail", "Invalid ID format", id);
            return Results.Json(ApiResponse<Metadata>.Fail($"Invalid Hanime ID: {id}"));
        }

        // First, check cache without acquiring concurrency slot
        var cachedResult = cache.TryGetCached("hanime", parsedId);
        if (cachedResult != null)
        {
            // Cache hit - return immediately without using concurrency slot
            logger.LogAlways("HanimeDetail", $"ID query completed: '{id}' -> Found (Cache hit)");
            
            // Log memory cleanup info (lighter version for cached responses)
            logger.LogResourceEvent("MemoryCleanup", "Hanime ID query served from cache");
            
            return Results.Json(ApiResponse<Metadata>.Ok(cachedResult));
        }

        // Cache miss - now we need to acquire concurrency slot
        var acquired = await hanimeLimiter.TryWaitAsync(0, ct).ConfigureAwait(false);
        if (!acquired)
        {
            logger.LogRateLimit("HanimeDetail");
            return Results.Json(ApiResponse<Metadata>.Fail("Service busy. Retry later."), statusCode: 429);
        }

        try
        {
            // Double-check cache after acquiring slot (in case another request cached it)
            cachedResult = cache.TryGetCached("hanime", parsedId);
            if (cachedResult != null)
            {
                logger.LogAlways("HanimeDetail", $"ID query completed: '{id}' -> Found (Cache hit after slot acquisition)");
                return Results.Json(ApiResponse<Metadata>.Ok(cachedResult));
            }

            // Cache still empty, fetch from provider
            var detailUrl = provider.BuildDetailUrlById(parsedId);
            var result = await provider.FetchDetailAsync(detailUrl, ct);
            
            // Cache the result (even if null)
            cache.SetCached("hanime", parsedId, result);

            if (result != null)
            {
                logger.LogAlways("HanimeDetail", $"ID query completed: '{id}' -> Found (Cache miss)");
                
                // Force garbage collection after ID query to release resources
                var memoryBefore = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var memoryAfter = GC.GetTotalMemory(true);
                var freedMemory = memoryBefore - memoryAfter;
                
                // Log memory cleanup info
                logger.LogResourceEvent("MemoryCleanup", $"Hanime ID query cleanup freed {freedMemory / 1024 / 1024}MB memory");
                
                return Results.Json(ApiResponse<Metadata>.Ok(result));
            }
            else
            {
                logger.LogAlways("HanimeDetail", $"ID query completed: '{id}' -> Not found (Cache miss)");
                
                // Force garbage collection after ID query even if not found
                var memoryBefore = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var memoryAfter = GC.GetTotalMemory(true);
                var freedMemory = memoryBefore - memoryAfter;
                
                // Log memory cleanup info
                logger.LogResourceEvent("MemoryCleanup", $"Hanime ID query cleanup freed {freedMemory / 1024 / 1024}MB memory");
                
                return Results.Json(ApiResponse<Metadata>.Fail($"Content not found: {id}"));
            }
        }
        finally
        {
            try { hanimeLimiter.Release(); } catch { }
        }
    }
    catch (OperationCanceledException)
    {
        logger.LogWarningLite("HanimeDetail", "Cancelled", id);
        return Results.Json(ApiResponse<Metadata>.Fail("Request cancelled"));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[HanimeDetail] ❌ {Id}", id);
        return Results.Json(ApiResponse<Metadata>.Fail($"Detail error: {ex.Message}"));
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
/// GET /api/dlsite/search?title=RJ123456&max=1 (will search for "RJ123456" as text, not as ID)
/// </example>
app.MapGet("/api/dlsite/search", async (
    string title,
    int? max,
    DlsiteProvider provider,
    ScraperBackendService.Core.Concurrency.DlsiteConcurrencyLimiter dlsiteLimiter,
    CancellationToken ct) =>
{
    var acquired = false;
    
    try
    {
        acquired = await dlsiteLimiter.TryWaitAsync(0, ct).ConfigureAwait(false);
        if (!acquired)
        {
            logger.LogRateLimit("DLsiteSearch");
            return Results.Json(ApiResponse<List<Metadata>>.Fail("Service busy. Retry later."), statusCode: 429);
        }

        // Always log search initiation - not affected by log level
        logger.LogAlways("DLsiteSearch", $"Title search started: '{title}'");

        var maxResults = Math.Min(max ?? 12, 50);
        var hits = await provider.SearchAsync(title, maxResults, ct).ConfigureAwait(false);

        var results = new List<Metadata>();
        
        // Remove internal concurrency control - rely on provider limits only
        var tasks = hits.Take(maxResults).Select(async hit =>
        {
            try
            {
                var detail = await provider.FetchDetailAsync(hit.DetailUrl, ct).ConfigureAwait(false);
                if (detail != null)
                {
                    lock (results)
                    {
                        results.Add(detail);
                    }
                }
            }
            catch (Exception)
            {
                logger.LogWarningLite("DetailFetch", "Failed", hit.DetailUrl);
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        
        // Always log search completion - not affected by log level
        logger.LogAlways("DLsiteSearch", $"Title search completed: '{title}' -> {results.Count} results");
        
        return Results.Json(ApiResponse<List<Metadata>>.Ok(results));
    }
    catch (OperationCanceledException)
    {
        logger.LogWarningLite("DLsiteSearch", "Cancelled", title);
        return Results.Json(ApiResponse<List<Metadata>>.Fail("Request cancelled"));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[DLsiteSearch] ❌ {Title}", title);
        return Results.Json(ApiResponse<List<Metadata>>.Fail($"Search error: {ex.Message}"));
    }
    finally
    {
        if (acquired)
        {
            try { dlsiteLimiter.Release(); } catch { }
        }
    }
});

/// <summary>
/// Get detailed information for a specific DLsite product by ID.
/// </summary>
/// <param name="id">DLsite product ID (e.g., "RJ123456", "VJ012345")</param>
/// <param name="provider">DLsite provider instance (injected)</param>
/// <param name="dlsiteLimiter">DLsite concurrency limiter (shared with search)</param>
/// <param name="cache">Metadata cache for preventing duplicate requests</param>
/// <param name="ct">Cancellation token</param>
/// <returns>Detailed DLsite metadata or error response</returns>
/// <example>
/// GET /api/dlsite/RJ123456
/// GET /api/dlsite/RJ01402281
/// </example>
app.MapGet("/api/dlsite/{id}", async (
    string id,
    DlsiteProvider provider,
    ScraperBackendService.Core.Concurrency.DlsiteConcurrencyLimiter dlsiteLimiter,
    MetadataCache cache,
    CancellationToken ct) =>
{
    try
    {
        // Always log ID query initiation - not affected by log level
        logger.LogAlways("DLsiteDetail", $"ID query started: '{id}'");

        if (!provider.TryParseId(id, out var parsedId))
        {
            logger.LogWarningLite("DLsiteDetail", "Invalid ID format", id);
            return Results.Json(ApiResponse<Metadata>.Fail($"Invalid DLsite ID: {id}"));
        }

        // First, check cache without acquiring concurrency slot
        var cachedResult = cache.TryGetCached("dlsite", parsedId);
        if (cachedResult != null)
        {
            // Cache hit - return immediately without using concurrency slot
            logger.LogAlways("DLsiteDetail", $"ID query completed: '{id}' -> Found (Cache hit)");
            
            // Log memory cleanup info (lighter version for cached responses)
            logger.LogResourceEvent("MemoryCleanup", "DLsite ID query served from cache");
            
            return Results.Json(ApiResponse<Metadata>.Ok(cachedResult));
        }

        // Cache miss - now we need to acquire concurrency slot
        var acquired = await dlsiteLimiter.TryWaitAsync(0, ct).ConfigureAwait(false);
        if (!acquired)
        {
            logger.LogRateLimit("DLsiteDetail");
            return Results.Json(ApiResponse<Metadata>.Fail("Service busy. Retry later."), statusCode: 429);
        }

        try
        {
            // Double-check cache after acquiring slot (in case another request cached it)
            cachedResult = cache.TryGetCached("dlsite", parsedId);
            if (cachedResult != null)
            {
                logger.LogAlways("DLsiteDetail", $"ID query completed: '{id}' -> Found (Cache hit after slot acquisition)");
                return Results.Json(ApiResponse<Metadata>.Ok(cachedResult));
            }

            // Cache still empty, fetch from provider
            var detailUrl = provider.BuildDetailUrlById(parsedId);
            var result = await provider.FetchDetailAsync(detailUrl, ct);
            
            // Cache the result (even if null)
            cache.SetCached("dlsite", parsedId, result);

            if (result != null)
            {
                logger.LogAlways("DLsiteDetail", $"ID query completed: '{id}' -> Found (Cache miss)");
                
                // Force garbage collection after ID query to release resources
                var memoryBefore = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var memoryAfter = GC.GetTotalMemory(true);
                var freedMemory = memoryBefore - memoryAfter;
                
                // Log memory cleanup info
                logger.LogResourceEvent("MemoryCleanup", $"DLsite ID query cleanup freed {freedMemory / 1024 / 1024}MB memory");
                
                return Results.Json(ApiResponse<Metadata>.Ok(result));
            }
            else
            {
                logger.LogAlways("DLsiteDetail", $"ID query completed: '{id}' -> Not found (Cache miss)");
                
                // Force garbage collection after ID query even if not found
                var memoryBefore = GC.GetTotalMemory(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var memoryAfter = GC.GetTotalMemory(true);
                var freedMemory = memoryBefore - memoryAfter;
                
                // Log memory cleanup info
                logger.LogResourceEvent("MemoryCleanup", $"DLsite ID query cleanup freed {freedMemory / 1024 / 1024}MB memory");
                
                return Results.Json(ApiResponse<Metadata>.Fail($"Content not found: {id}"));
            }
        }
        finally
        {
            try { dlsiteLimiter.Release(); } catch { }
        }
    }
    catch (OperationCanceledException)
    {
        logger.LogWarningLite("DLsiteDetail", "Cancelled", id);
        return Results.Json(ApiResponse<Metadata>.Fail("Request cancelled"));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[DLsiteDetail] ❌ {Id}", id);
        return Results.Json(ApiResponse<Metadata>.Fail($"Detail error: {ex.Message}"));
    }
});

// Build and start the service
var listenUrl = $"http://{serviceConfig.Host}:{serviceConfig.Port}";

// Always log startup info regardless of logging level
logger.LogAlways("ServiceStartup", $"Listening on {listenUrl}");
logger.LogAlways("ServiceStartup", string.IsNullOrWhiteSpace(serviceConfig.AuthToken) ? "Authentication: Disabled" : "Authentication: Enabled", listenUrl);

// Register shutdown handlers to always log service stop events
var lifetime = app.Lifetime;
lifetime.ApplicationStopping.Register(() => logger.LogAlways("ServiceShutdown", "Stopping service"));
lifetime.ApplicationStopped.Register(() => logger.LogAlways("ServiceShutdown", "Service stopped"));

Console.CancelKeyPress += (_, e) =>
{
    logger.LogAlways("ServiceShutdown", "CancelKeyPress received - stopping");
    // allow default behavior
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => logger.LogAlways("ServiceShutdown", "Process exiting");

 try
 {
     app.Run(listenUrl);
 }
 catch (Exception ex)
 {
     logger.LogFailure("ServiceStartup", "Service failed to start", listenUrl, ex);
     throw;
 }
