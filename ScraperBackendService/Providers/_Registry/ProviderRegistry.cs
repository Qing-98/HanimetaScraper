using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScraperBackendService.Configuration;
using ScraperBackendService.Core.Concurrency;
using ScraperBackendService.Core.Caching;
using ScraperBackendService.Core.Abstractions;
using ScraperBackendService.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ScraperBackendService.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScraperBackendService.Providers._Registry;

/// <summary>
/// Central registry for all scraping providers.
/// Handles provider registration, dependency injection setup, and API endpoint mapping.
/// </summary>
public static class ProviderRegistry
{
    /// <summary>
    /// Gets all registered provider configurations.
    /// Add new providers here by including their configuration classes.
    /// </summary>
    public static IEnumerable<IProviderConfig> GetAllProviders()
    {
        yield return new Hanime.HanimeProviderConfig();
        yield return new DLsite.DlsiteProviderConfig();
        
        // Add new providers here:
        // yield return new YourProvider.YourProviderConfig();
    }

    /// <summary>
    /// Registers a provider with dependency injection container.
    /// Sets up concurrency limiter, rate limiter, and provider instance.
    /// </summary>
    public static void RegisterProvider(
        this IServiceCollection services,
        IProviderConfig config,
        ServiceConfiguration serviceConfig)
    {
        var providerName = config.ProviderName;

        // Create and register concurrency limiter
        var concurrencyLimiter = new ProviderConcurrencyLimiter(
            serviceConfig.MaxConcurrentRequests, 
            providerName);
        services.AddSingleton(concurrencyLimiter);
        services.AddKeyedSingleton($"{providerName}ConcurrencyLimiter", concurrencyLimiter);

        // Create and register rate limiter
        var rateLimiter = new ProviderRateLimiter(
            TimeSpan.FromSeconds(serviceConfig.RateLimitSeconds), 
            providerName);
        services.AddSingleton(rateLimiter);
        services.AddKeyedSingleton($"{providerName}RateLimiter", rateLimiter);

        // Register provider with factory method
        services.AddScoped(config.ProviderType, sp =>
        {
            var loggerType = typeof(ILogger<>).MakeGenericType(config.ProviderType);
            var logger = (ILogger)sp.GetService(loggerType)!;
            return config.CreateProvider(sp, logger);
        });
    }

    /// <summary>
    /// Maps API endpoints for a provider.
    /// Creates /api/{prefix}/search and /api/{prefix}/{id} endpoints.
    /// </summary>
    public static void MapProviderEndpoints(
        this WebApplication app,
        IProviderConfig config,
        ILogger logger)
    {
        var providerName = config.ProviderName;
        var routePrefix = config.RoutePrefix;
        var cacheKey = config.CacheKey;

        // Search endpoint: GET /api/{prefix}/search?title={query}&max={limit}
        app.MapGet($"/api/{routePrefix}/search", async (
            string title,
            int? max,
            IServiceProvider sp,
            CancellationToken ct) =>
        {
            return await HandleSearchRequest(
                sp, config, title, max, logger, providerName, cacheKey, ct);
        });

        // Detail endpoint: GET /api/{prefix}/{id}
        app.MapGet($"/api/{routePrefix}/{{id}}", async (
            string id,
            IServiceProvider sp,
            CancellationToken ct) =>
        {
            return await HandleDetailRequest(
                sp, config, id, logger, providerName, cacheKey, ct);
        });
    }

    #region Request Handlers

    private static async Task<IResult> HandleSearchRequest(
        IServiceProvider sp,
        IProviderConfig config,
        string title,
        int? max,
        ILogger logger,
        string providerName,
        string cacheKey,
        CancellationToken ct)
    {
        var provider = (IMediaProvider)sp.GetRequiredService(config.ProviderType);
        var limiter = sp.GetRequiredKeyedService<ProviderConcurrencyLimiter>($"{providerName}ConcurrencyLimiter");
        var rateLimiter = sp.GetRequiredKeyedService<ProviderRateLimiter>($"{providerName}RateLimiter");

        ConcurrencySlot? slot = null;
        try
        {
            var maxResults = Math.Min(max ?? 12, 50);
            logger.LogAlways($"{providerName}Search", $"Searching: '{title}' (max: {maxResults})");

            slot = await limiter.TryWaitAndAcquireSlotAsync(15000, ct).ConfigureAwait(false);
            if (slot == null)
            {
                logger.LogRateLimit($"{providerName}Search");
                return Results.Json(
                    ApiResponse<List<Metadata>>.Fail("Service busy. All concurrency slots occupied. Please retry later."),
                    statusCode: 429);
            }

            var waitTime = rateLimiter.GetWaitTime(slot.SlotId);
            if (waitTime > TimeSpan.Zero)
            {
                logger.LogDebug($"{providerName}Search", $"Rate limit wait: {waitTime.TotalSeconds:F1}s", title);
            }

            await rateLimiter.WaitIfNeededAsync(slot.SlotId, ct).ConfigureAwait(false);

            var hits = await provider.SearchAsync(title, maxResults, ct).ConfigureAwait(false);

            // Log search results count
            if (hits.Count > 0)
            {
                logger.LogAlways($"{providerName}Search", $"Found {hits.Count} results", title);
            }
            else
            {
                logger.LogAlways($"{providerName}Search", "No results found", title);
                // Record successful rate limit completion even for zero results
                rateLimiter.RecordRequestComplete(slot.SlotId);
                return Results.Json(ApiResponse<List<Metadata>>.Ok(new List<Metadata>()));
            }

            // Use OrderedAsync.ForEachAsync to limit concurrent detail fetching to 4-6 tasks
            // This prevents overwhelming the target server with too many simultaneous requests
            var results = await ScraperBackendService.Core.Util.OrderedAsync.ForEachAsync(
                hits.Take(maxResults).ToList(),
                degree: 4, // Limit to 4 concurrent detail fetches
                async hit =>
                {
                    try
                    {
                        var detail = await provider.FetchDetailAsync(hit.DetailUrl, ct).ConfigureAwait(false);
                        return detail;
                    }
                    catch (Exception)
                    {
                        logger.LogWarningLite("DetailFetch", "Failed", hit.DetailUrl);
                        return null;
                    }
                });

            // Record successful rate limit completion
            rateLimiter.RecordRequestComplete(slot.SlotId);

            // Log final results count
            var validResults = results.Where(r => r != null).ToList();
            logger.LogAlways($"{providerName}Search", $"Retrieved {validResults.Count}/{hits.Count} details");

            return Results.Json(ApiResponse<List<Metadata>>.Ok(validResults));
        }
        catch (OperationCanceledException)
        {
            logger.LogWarningLite($"{providerName}Search", "Cancelled", title);
            return Results.Json(ApiResponse<List<Metadata>>.Fail("Request cancelled"));
        }
        catch (Exception ex)
        {
            logger.LogFailure($"{providerName}Search", $"Search error: {ex.Message}", title, ex);
            return Results.Json(ApiResponse<List<Metadata>>.Fail($"Search error: {ex.Message}"));
        }
        finally
        {
            slot?.Dispose();
        }
    }

    private static async Task<IResult> HandleDetailRequest(
        IServiceProvider sp,
        IProviderConfig config,
        string id,
        ILogger logger,
        string providerName,
        string cacheKey,
        CancellationToken ct)
    {
        var provider = (IMediaProvider)sp.GetRequiredService(config.ProviderType);
        var limiter = sp.GetRequiredKeyedService<ProviderConcurrencyLimiter>($"{providerName}ConcurrencyLimiter");
        var rateLimiter = sp.GetRequiredKeyedService<ProviderRateLimiter>($"{providerName}RateLimiter");
        var cache = sp.GetRequiredService<MetadataCache>();

        ConcurrencySlot? slot = null;
        try
        {
            logger.LogAlways($"{providerName}Detail", $"Querying ID: {id}");

            if (!provider.TryParseId(id, out var parsedId))
            {
                logger.LogAlways($"{providerName}Detail", $"Invalid ID format: {id}");
                return Results.Json(ApiResponse<Metadata>.Fail($"Invalid {providerName} ID: {id}"));
            }

            // First cache check: Fast path without acquiring slot
            // Avoid wasting concurrency slots for cached items
            var cachedResult = cache.TryGetCached(cacheKey, parsedId);
            if (cachedResult != null)
            {
                logger.LogAlways($"{providerName}Detail", "Cache hit", parsedId);
                return Results.Json(ApiResponse<Metadata>.Ok(cachedResult));
            }

            // Acquire concurrency slot for actual scraping
            slot = await limiter.TryWaitAndAcquireSlotAsync(15000, ct).ConfigureAwait(false);
            if (slot == null)
            {
                logger.LogRateLimit($"{providerName}Detail");
                return Results.Json(
                    ApiResponse<Metadata>.Fail("Service busy. All concurrency slots occupied. Please retry later."),
                    statusCode: 429);
            }

            logger.LogDebug($"{providerName}Detail", $"Slot {slot.SlotId} acquired", parsedId);

            // Second cache check: Critical for preventing duplicate scraping
            // While we were waiting for slot, another request may have cached the result
            // This is the "double-checked locking" pattern for concurrent scenarios
            cachedResult = cache.TryGetCached(cacheKey, parsedId);
            if (cachedResult != null)
            {
                logger.LogAlways($"{providerName}Detail", "Cache hit (after slot wait)", parsedId);
                return Results.Json(ApiResponse<Metadata>.Ok(cachedResult));
            }

            var waitTime = rateLimiter.GetWaitTime(slot.SlotId);
            if (waitTime > TimeSpan.Zero)
            {
                logger.LogDebug($"{providerName}Detail", $"Slot {slot.SlotId} waiting {waitTime.TotalSeconds:F1}s (rate limit)", parsedId);
            }
            else
            {
                logger.LogDebug($"{providerName}Detail", $"Slot {slot.SlotId} no wait needed (first request or interval passed)", parsedId);
            }

            await rateLimiter.WaitIfNeededAsync(slot.SlotId, ct).ConfigureAwait(false);

            var detailUrl = provider.BuildDetailUrlById(parsedId);
            logger.LogDebug($"{providerName}Detail", $"Fetching: {detailUrl}", parsedId);
            
            var result = await provider.FetchDetailAsync(detailUrl, ct).ConfigureAwait(false);

            // Record rate limit completion after successful fetch to enforce minimum interval
            rateLimiter.RecordRequestComplete(slot.SlotId);
            logger.LogDebug($"{providerName}Detail", $"Slot {slot.SlotId} request complete, timestamp recorded", parsedId);

            cache.SetCached(cacheKey, parsedId, result);

            if (result != null)
            {
                logger.LogAlways($"{providerName}Detail", "Success", parsedId);
                return Results.Json(ApiResponse<Metadata>.Ok(result));
            }
            else
            {
                logger.LogAlways($"{providerName}Detail", "Content not found", parsedId);
                return Results.Json(ApiResponse<Metadata>.Fail($"Content not found: {id}"));
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarningLite($"{providerName}Detail", "Cancelled", id);
            return Results.Json(ApiResponse<Metadata>.Fail("Request cancelled"));
        }
        catch (Exception ex)
        {
            logger.LogFailure($"{providerName}Detail", $"Detail error: {ex.Message}", id, ex);
            return Results.Json(ApiResponse<Metadata>.Fail($"Detail error: {ex.Message}"));
        }
        finally
        {
            if (slot != null)
            {
                logger.LogDebug($"{providerName}Detail", $"Slot {slot.SlotId} released", id);
            }
            slot?.Dispose();
        }
    }

    #endregion
}
