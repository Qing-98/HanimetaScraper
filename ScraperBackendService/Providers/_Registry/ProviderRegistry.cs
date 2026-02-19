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
        var cache = sp.GetRequiredService<MetadataCache>();

        var maxResults = Math.Min(max ?? 12, 50);
        logger.LogAlways($"{providerName}Search", $"Searching: '{title}' (max: {maxResults})");

        // ── Phase 1: fetch the search results page (slot + rate limited) ──
        IReadOnlyList<SearchHit> hits;
        try
        {
            var searchSlot = await limiter.TryWaitAndAcquireSlotAsync(15_000, ct).ConfigureAwait(false);
            if (searchSlot == null)
            {
                logger.LogRateLimit($"{providerName}Search");
                return Results.Json(
                    ApiResponse<List<Metadata>>.Fail("Service busy. All concurrency slots occupied. Please retry later."),
                    statusCode: 429);
            }

            try
            {
                var waitTime = rateLimiter.GetWaitTime(searchSlot.SlotId);
                if (waitTime > TimeSpan.Zero)
                    logger.LogDebug($"{providerName}Search", $"Rate limit wait: {waitTime.TotalSeconds:F1}s", title);

                await rateLimiter.WaitIfNeededAsync(searchSlot.SlotId, ct).ConfigureAwait(false);
                hits = await provider.SearchAsync(title, maxResults, ct).ConfigureAwait(false);
                rateLimiter.RecordRequestComplete(searchSlot.SlotId);
            }
            finally
            {
                // Always release the search slot before expanding to detail pages,
                // so detail fetches (below) can compete fairly with other API requests.
                searchSlot.Dispose();
            }
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

        if (hits.Count == 0)
        {
            logger.LogAlways($"{providerName}Search", "No results found", title);
            return Results.Json(ApiResponse<List<Metadata>>.Ok(new List<Metadata>()));
        }

        logger.LogAlways($"{providerName}Search", $"Found {hits.Count} results", title);

        // ── Phase 2: expand each hit to full detail (slot limited, cache-aware) ──
        // Each hit goes through FetchDetailWithCacheAsync — the same logic used by the
        // direct detail endpoint — with two differences:
        //   applyRateLimit: false  (rate was already paid by the search phase)
        //   slotTimeoutMs: 30_000  (worth waiting longer; we already have the hit list)
        List<Metadata> results;
        try
        {
            results = await ScraperBackendService.Core.Util.OrderedAsync.ForEachAsync(
                hits.Take(maxResults).ToList(),
                degree: limiter.MaxCount, // match slot pool size — no point spawning more workers than available slots
                async hit =>
                {
                    if (!provider.TryParseId(hit.DetailUrl, out var hitId))
                        return null;
                    try
                    {
                        return await FetchDetailWithCacheAsync(
                            provider, limiter, rateLimiter, cache, cacheKey,
                            hitId, hit.DetailUrl, slotTimeoutMs: 30_000,
                            logger, $"{providerName}DetailExpand", ct).ConfigureAwait(false);
                    }
                    catch (InvalidOperationException ex) when (ex.Message.StartsWith("Slot busy"))
                    {
                        logger.LogWarningLite("DetailFetch", "Slot timeout, skipping", hit.DetailUrl);
                        return null;
                    }
                    catch (Exception)
                    {
                        logger.LogWarningLite("DetailFetch", "Failed", hit.DetailUrl);
                        return null;
                    }
                });
        }
        catch (OperationCanceledException)
        {
            logger.LogWarningLite($"{providerName}Search", "Cancelled during detail fetch", title);
            return Results.Json(ApiResponse<List<Metadata>>.Fail("Request cancelled"));
        }
        catch (Exception ex)
        {
            logger.LogFailure($"{providerName}Search", $"Detail fetch error: {ex.Message}", title, ex);
            return Results.Json(ApiResponse<List<Metadata>>.Fail($"Detail fetch error: {ex.Message}"));
        }

        var validResults = results.Where(r => r != null).Select(r => r!).ToList();
        logger.LogAlways($"{providerName}Search", $"Retrieved {validResults.Count}/{hits.Count} details");
        return Results.Json(ApiResponse<List<Metadata>>.Ok(validResults));
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

        logger.LogAlways($"{providerName}Detail", $"Querying ID: {id}");

        if (!provider.TryParseId(id, out var parsedId))
        {
            logger.LogAlways($"{providerName}Detail", $"Invalid ID format: {id}");
            return Results.Json(ApiResponse<Metadata>.Fail($"Invalid {providerName} ID: {id}"));
        }

        try
        {
            var detailUrl = provider.BuildDetailUrlById(parsedId);
            var result = await FetchDetailWithCacheAsync(
                provider, limiter, rateLimiter, cache, cacheKey,
                parsedId, detailUrl, slotTimeoutMs: 15_000,
                logger, $"{providerName}Detail", ct).ConfigureAwait(false);

            if (result != null)
            {
                logger.LogAlways($"{providerName}Detail", "Success", parsedId);
                return Results.Json(ApiResponse<Metadata>.Ok(result));
            }
            else
            {
                logger.LogAlways($"{providerName}Detail", "Content not found", parsedId);
                return Results.Json(ApiResponse<Metadata>.Fail($"Content not found: {id}"), statusCode: 404);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarningLite($"{providerName}Detail", "Cancelled", id);
            return Results.Json(ApiResponse<Metadata>.Fail("Request cancelled"));
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Slot busy"))
        {
            logger.LogRateLimit($"{providerName}Detail");
            return Results.Json(
                ApiResponse<Metadata>.Fail("Service busy. All concurrency slots occupied. Please retry later."),
                statusCode: 429);
        }
        catch (Exception ex)
        {
            logger.LogFailure($"{providerName}Detail", $"Detail error: {ex.Message}", id, ex);
            return Results.Json(ApiResponse<Metadata>.Fail($"Detail error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Core detail fetch logic shared by the detail endpoint and search-internal expansion.
    /// Implements double-checked cache, slot acquisition, rate limiting, and cache write.
    /// Rate limiting is always applied: any caller that acquires a slot is subject to the
    /// per-slot interval, ensuring consistent throttling regardless of call origin.
    /// </summary>
    /// <param name="slotTimeoutMs">
    /// How long to wait for a free slot before giving up.
    /// Direct detail requests use a shorter timeout (fail fast with 429);
    /// search-internal fetches use a longer timeout (worth waiting in queue).
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown with "Slot busy" prefix when no slot is available within the timeout.</exception>
    private static async Task<Metadata?> FetchDetailWithCacheAsync(
        IMediaProvider provider,
        ProviderConcurrencyLimiter limiter,
        ProviderRateLimiter rateLimiter,
        MetadataCache cache,
        string cacheKey,
        string parsedId,
        string detailUrl,
        int slotTimeoutMs,
        ILogger logger,
        string logOperation,
        CancellationToken ct)
    {
        // Pre-slot cache check — avoids acquiring a slot for already-cached items
        var cached = cache.TryGetCached(cacheKey, parsedId);
        if (cached != null)
        {
            logger.LogDebug(logOperation, "Cache hit (pre-slot)", parsedId);
            return cached;
        }

        var slot = await limiter.TryWaitAndAcquireSlotAsync(slotTimeoutMs, ct).ConfigureAwait(false);
        if (slot == null)
            throw new InvalidOperationException("Slot busy");

        try
        {
            // Post-slot cache check — guards against parallel workers fetching the same ID
            cached = cache.TryGetCached(cacheKey, parsedId);
            if (cached != null)
            {
                logger.LogDebug(logOperation, "Cache hit (post-slot)", parsedId);
                return cached;
            }

            var waitTime = rateLimiter.GetWaitTime(slot.SlotId);
            if (waitTime > TimeSpan.Zero)
                logger.LogDebug(logOperation, $"Slot {slot.SlotId} rate limit wait: {waitTime.TotalSeconds:F1}s", parsedId);
            await rateLimiter.WaitIfNeededAsync(slot.SlotId, ct).ConfigureAwait(false);

            logger.LogDebug(logOperation, $"Slot {slot.SlotId} fetching", parsedId);
            var result = await provider.FetchDetailAsync(detailUrl, ct).ConfigureAwait(false);

            rateLimiter.RecordRequestComplete(slot.SlotId);
            cache.SetCached(cacheKey, parsedId, result);
            return result;
        }
        finally
        {
            logger.LogDebug(logOperation, $"Slot {slot.SlotId} released", parsedId);
            slot.Dispose();
        }
    }

    #endregion
}
