using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScraperBackendService.Configuration;
using ScraperBackendService.Core.Abstractions;
using ScraperBackendService.Core.Net;
using ScraperBackendService.Core.Concurrency;
using ScraperBackendService.Core.Caching;
using ScraperBackendService.Core.Logging;
using ScraperBackendService.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScraperBackendService.Extensions;

/// <summary>
/// Builder for configuring provider registration information.
/// Provides a centralized way to register new scraping providers with all their dependencies.
/// </summary>
public static class ProviderRegistrationBuilder
{
    /// <summary>
    /// Creates pre-configured registration for Hanime provider.
    /// </summary>
    public static ProviderRegistration CreateForHanime()
    {
        return new ProviderRegistration
        {
            ProviderName = "Hanime",
            RoutePrefix = "hanime",
            CacheKey = "hanime",
            ProviderType = typeof(ScraperBackendService.Providers.Hanime.HanimeProvider),
            NetworkClientType = typeof(PlaywrightNetworkClient),
            GetConcurrencyLimitFunc = config => config.MaxConcurrentRequests,
            GetRateLimitSecondsFunc = config => config.RateLimitSeconds,
            ProviderFactory = (sp, logger) =>
            {
                var client = sp.GetRequiredService<PlaywrightNetworkClient>();
                return new ScraperBackendService.Providers.Hanime.HanimeProvider(client, (ILogger<ScraperBackendService.Providers.Hanime.HanimeProvider>)logger);
            }
        };
    }

    /// <summary>
    /// Creates pre-configured registration for DLsite provider.
    /// </summary>
    public static ProviderRegistration CreateForDLsite()
    {
        return new ProviderRegistration
        {
            ProviderName = "DLsite",
            RoutePrefix = "dlsite",
            CacheKey = "dlsite",
            ProviderType = typeof(ScraperBackendService.Providers.DLsite.DlsiteProvider),
            NetworkClientType = typeof(HttpNetworkClient),
            GetConcurrencyLimitFunc = config => config.MaxConcurrentRequests,
            GetRateLimitSecondsFunc = config => config.RateLimitSeconds,
            ProviderFactory = (sp, logger) =>
            {
                var client = sp.GetRequiredService<HttpNetworkClient>();
                return new ScraperBackendService.Providers.DLsite.DlsiteProvider(client, (ILogger<ScraperBackendService.Providers.DLsite.DlsiteProvider>)logger);
            }
        };
    }

    /// <summary>
    /// Registers a provider with all its dependencies (concurrency limiter, rate limiter, and provider itself).
    /// </summary>
    public static void RegisterProvider(
        this IServiceCollection services,
        ProviderRegistration registration,
        ServiceConfiguration config)
    {
        if (registration == null)
            throw new ArgumentNullException(nameof(registration));

        var providerName = registration.ProviderName;
        
        // Get configured limits
        var concurrencyLimit = registration.GetConcurrencyLimitFunc(config);
        var rateLimitSeconds = registration.GetRateLimitSecondsFunc(config);

        // Register concurrency limiter with provider name
        var concurrencyLimiter = new ProviderConcurrencyLimiter(concurrencyLimit, providerName);
        services.AddSingleton(concurrencyLimiter);
        
        // Also register with a keyed service for easy retrieval
        services.AddKeyedSingleton($"{providerName}ConcurrencyLimiter", concurrencyLimiter);

        // Register rate limiter with provider name
        var rateLimiter = new ProviderRateLimiter(TimeSpan.FromSeconds(rateLimitSeconds), providerName);
        services.AddSingleton(rateLimiter);
        
        // Also register with a keyed service for easy retrieval
        services.AddKeyedSingleton($"{providerName}RateLimiter", rateLimiter);

        // Register provider with factory method
        services.AddScoped(registration.ProviderType, sp =>
        {
            var loggerType = typeof(ILogger<>).MakeGenericType(registration.ProviderType);
            var logger = (ILogger)sp.GetService(loggerType)!;
            return registration.ProviderFactory(sp, logger);
        });
    }

    /// <summary>
    /// Maps API endpoints for a provider.
    /// </summary>
    public static void MapProviderEndpoints(
        this WebApplication app,
        ProviderRegistration registration,
        ILogger logger)
    {
        var providerName = registration.ProviderName;
        var routePrefix = registration.RoutePrefix;
        var cacheKey = registration.CacheKey;

        // Search endpoint
        app.MapGet($"/api/{routePrefix}/search", async (
            string title,
            int? max,
            IServiceProvider sp,
            CancellationToken ct) =>
        {
            return await HandleSearchRequest(
                sp, registration, title, max, logger, providerName, cacheKey, ct);
        });

        // Detail endpoint
        app.MapGet($"/api/{routePrefix}/{{id}}", async (
            string id,
            IServiceProvider sp,
            CancellationToken ct) =>
        {
            return await HandleDetailRequest(
                sp, registration, id, logger, providerName, cacheKey, ct);
        });
    }

    private static async Task<IResult> HandleSearchRequest(
        IServiceProvider sp,
        ProviderRegistration registration,
        string title,
        int? max,
        ILogger logger,
        string providerName,
        string cacheKey,
        CancellationToken ct)
    {
        var provider = (IMediaProvider)sp.GetRequiredService(registration.ProviderType);
        
        // Get limiters by keyed service
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
                logger.LogAlways($"{providerName}Search", $"Waiting {waitTime.TotalSeconds:F0}s (rate limit)");
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

            // Use OrderedAsync.ForEachAsync to limit concurrent detail fetching to 4 tasks
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
        ProviderRegistration registration,
        string id,
        ILogger logger,
        string providerName,
        string cacheKey,
        CancellationToken ct)
    {
        var provider = (IMediaProvider)sp.GetRequiredService(registration.ProviderType);
        
        // Get limiters by keyed service
        var limiter = sp.GetRequiredKeyedService<ProviderConcurrencyLimiter>($"{providerName}ConcurrencyLimiter");
        var rateLimiter = sp.GetRequiredKeyedService<ProviderRateLimiter>($"{providerName}RateLimiter");
        var cache = sp.GetRequiredService<MetadataCache>();

        ConcurrencySlot? slot = null;
        try
        {
            logger.LogAlways($"{providerName}Detail", $"Query: '{id}'");

            if (!provider.TryParseId(id, out var parsedId))
            {
                logger.LogWarningLite($"{providerName}Detail", "Invalid ID format", id);
                return Results.Json(ApiResponse<Metadata>.Fail($"Invalid {providerName} ID: {id}"));
            }

            var cachedResult = cache.TryGetCached(cacheKey, parsedId);
            if (cachedResult != null)
            {
                logger.LogAlways($"{providerName}Detail", $"✅ Found (cache)");
                return Results.Json(ApiResponse<Metadata>.Ok(cachedResult));
            }

            slot = await limiter.TryWaitAndAcquireSlotAsync(15000, ct).ConfigureAwait(false);
            if (slot == null)
            {
                logger.LogRateLimit($"{providerName}Detail");
                return Results.Json(
                    ApiResponse<Metadata>.Fail("Service busy. All concurrency slots occupied. Please retry later."),
                    statusCode: 429);
            }

            cachedResult = cache.TryGetCached(cacheKey, parsedId);
            if (cachedResult != null)
            {
                logger.LogAlways($"{providerName}Detail", $"✅ Found (cache)");
                return Results.Json(ApiResponse<Metadata>.Ok(cachedResult));
            }

            var waitTime = rateLimiter.GetWaitTime(slot.SlotId);
            if (waitTime > TimeSpan.Zero)
            {
                logger.LogAlways($"{providerName}Detail", $"Waiting {waitTime.TotalSeconds:F0}s (rate limit)");
            }

            await rateLimiter.WaitIfNeededAsync(slot.SlotId, ct).ConfigureAwait(false);

            var detailUrl = provider.BuildDetailUrlById(parsedId);
            var result = await provider.FetchDetailAsync(detailUrl, ct).ConfigureAwait(false);

            // Record rate limit completion after successful fetch to enforce minimum interval
            rateLimiter.RecordRequestComplete(slot.SlotId);

            cache.SetCached(cacheKey, parsedId, result);

            if (result != null)
            {
                logger.LogAlways($"{providerName}Detail", "✅ Found");
                return Results.Json(ApiResponse<Metadata>.Ok(result));
            }
            else
            {
                logger.LogAlways($"{providerName}Detail", "❌ Not found");
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
            slot?.Dispose();
        }
    }
}

/// <summary>
/// Configuration information for registering a scraping provider.
/// Contains all necessary metadata for provider registration and dependency injection setup.
/// </summary>
public class ProviderRegistration
{
    /// <summary>
    /// Gets the provider name (e.g., "Hanime", "DLsite").
    /// Used for logging and identification.
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// Gets the route prefix for API endpoints (e.g., "hanime", "dlsite").
    /// Used in /api/{routePrefix}/search and /api/{routePrefix}/{id}.
    /// </summary>
    public required string RoutePrefix { get; init; }

    /// <summary>
    /// Gets the cache key prefix for this provider.
    /// </summary>
    public required string CacheKey { get; init; }

    /// <summary>
    /// Gets the provider type (e.g., HanimeProvider, DlsiteProvider).
    /// </summary>
    public required Type ProviderType { get; init; }

    /// <summary>
    /// Gets the network client type used by this provider.
    /// </summary>
    public required Type NetworkClientType { get; init; }

    /// <summary>
    /// Gets the function to extract concurrency limit from configuration.
    /// </summary>
    public required Func<ServiceConfiguration, int> GetConcurrencyLimitFunc { get; init; }

    /// <summary>
    /// Gets the function to extract rate limit seconds from configuration.
    /// </summary>
    public required Func<ServiceConfiguration, int> GetRateLimitSecondsFunc { get; init; }

    /// <summary>
    /// Gets the factory method to create the provider instance.
    /// Receives IServiceProvider and ILogger as parameters.
    /// </summary>
    public required Func<IServiceProvider, ILogger, object> ProviderFactory { get; init; }
}
