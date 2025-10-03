using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ScraperBackendService.Core.Logging;

/// <summary>
/// Centralized logger with structured logging for scraper operations.
/// Provides consistent logging format and request tracking with clear log levels.
/// </summary>
public static class ScraperLogger
{
    private static readonly ActivitySource ActivitySource = new("ScraperBackendService");

    /// <summary>
    /// Log successful operation completion (Information level).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogSuccess(this ILogger logger, string operation, string? identifier = null, int? itemCount = null)
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;
        
        if (identifier != null && itemCount.HasValue)
        {
            logger.LogInformation("[{Operation}] ✅ {Identifier} -> {ItemCount} items", operation, identifier, itemCount.Value);
        }
        else if (identifier != null)
        {
            logger.LogInformation("[{Operation}] ✅ {Identifier}", operation, identifier);
        }
        else
        {
            logger.LogInformation("[{Operation}] ✅ Completed", operation);
        }
    }

    /// <summary>
    /// Log operation warning (Warning level).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWarning(this ILogger logger, string operation, string warning, string? identifier = null, Exception? ex = null)
    {
        if (!logger.IsEnabled(LogLevel.Warning)) return;
        
        var message = identifier != null
            ? "[{Operation}] ⚠️ {Identifier} | {Warning}"
            : "[{Operation}] ⚠️ {Warning}";

        if (ex != null)
        {
            if (identifier != null)
            {
                logger.LogWarning(ex, message, operation, identifier, warning);
            }
            else
            {
                logger.LogWarning(ex, message, operation, warning);
            }
        }
        else
        {
            if (identifier != null)
            {
                logger.LogWarning(message, operation, identifier, warning);
            }
            else
            {
                logger.LogWarning(message, operation, warning);
            }
        }
    }

    /// <summary>
    /// Log operation failure (Error level).
    /// </summary>
    public static void LogFailure(this ILogger logger, string operation, string error, string? identifier = null, Exception? ex = null)
    {
        if (!logger.IsEnabled(LogLevel.Error)) return;

        var message = identifier != null
            ? "[{Operation}] ❌ {Identifier} | {Error}"
            : "[{Operation}] ❌ {Error}";

        if (ex != null)
        {
            if (identifier != null)
            {
                logger.LogError(ex, message, operation, identifier, error);
            }
            else
            {
                logger.LogError(ex, message, operation, error);
            }
        }
        else
        {
            if (identifier != null)
            {
                logger.LogError(message, operation, identifier, error);
            }
            else
            {
                logger.LogError(message, operation, error);
            }
        }
    }

    /// <summary>
    /// Log debug information (Debug level).
    /// Use sparingly for detailed troubleshooting information.
    /// </summary>
    public static void LogDebug(this ILogger logger, string operation, string info, string? identifier = null)
    {
        if (!logger.IsEnabled(LogLevel.Debug)) return;

        var message = identifier != null
            ? "[{Operation}] 🔍 {Identifier} | {Info}"
            : "[{Operation}] 🔍 {Info}";

        if (identifier != null)
        {
            logger.LogDebug(message, operation, identifier, info);
        }
        else
        {
            logger.LogDebug(message, operation, info);
        }
    }

    /// <summary>
    /// Log rate limiting events (Warning level).
    /// </summary>
    public static void LogRateLimit(this ILogger logger, string operation)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("[{Operation}] 🚦 Rate limited - Service busy", operation);
        }
    }

    /// <summary>
    /// Log resource management events (Debug level).
    /// </summary>
    public static void LogResourceEvent(this ILogger logger, string resource, string action, string? identifier = null)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            var message = identifier != null
                ? "[Resource] {Resource} {Action} | {Identifier}"
                : "[Resource] {Resource} {Action}";
            
            if (identifier != null)
            {
                logger.LogDebug(message, resource, action, identifier);
            }
            else
            {
                logger.LogDebug(message, resource, action);
            }
        }
    }

    /// <summary>
    /// Always-visible logging for critical system events.
    /// Writes directly to console and attempts logging through ILogger.
    /// Use only for startup, shutdown, and critical status messages.
    /// </summary>
    public static void LogAlways(this ILogger? logger, string operation, string message, string? identifier = null)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = identifier != null 
                ? $"{timestamp} [{operation}] {identifier} | {message}"
                : $"{timestamp} [{operation}] {message}";
            
            Console.WriteLine(logMessage);
        }
        catch
        {
            // Console write should never crash the app
        }

        try
        {
            // Also attempt to log through the standard logger
            if (identifier != null)
            {
                logger?.LogInformation("[{Operation}] {Identifier} | {Message}", operation, identifier, message);
            }
            else
            {
                logger?.LogInformation("[{Operation}] {Message}", operation, message);
            }
        }
        catch
        {
            // Ignore logging failures
        }
    }

    /// <summary>
    /// Lightweight logging for high-frequency operations (Information level).
    /// Optimized to avoid string allocations when logging is disabled.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogSuccessLite(this ILogger logger, string operation, string identifier)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("[{Operation}] ✅ {Identifier}", operation, identifier);
        }
    }

    /// <summary>
    /// Lightweight warning logging for high-frequency operations (Warning level).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWarningLite(this ILogger logger, string operation, string warning, string identifier)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("[{Operation}] ⚠️ {Identifier} | {Warning}", operation, identifier, warning);
        }
    }

    /// <summary>
    /// Log request start with timing.
    /// Returns a disposable scope that logs completion time when disposed.
    /// </summary>
    public static IDisposable LogRequestStart(this ILogger logger, string operation, string? identifier = null)
    {
        var activity = ActivitySource.StartActivity($"Scraper.{operation}");
        activity?.SetTag("operation", operation);
        if (identifier != null) activity?.SetTag("identifier", identifier);
        
        var stopwatch = Stopwatch.StartNew();
        
        if (logger.IsEnabled(LogLevel.Information))
        {
            var message = identifier != null 
                ? "[{Operation}] 🚀 Starting | {Identifier}"
                : "[{Operation}] 🚀 Starting";
            
            logger.LogInformation(message, operation, identifier);
        }

        return new RequestScope(logger, operation, identifier, activity, stopwatch);
    }

    private class RequestScope : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operation;
        private readonly string? _identifier;
        private readonly Activity? _activity;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public RequestScope(ILogger logger, string operation, string? identifier, Activity? activity, Stopwatch stopwatch)
        {
            _logger = logger;
            _operation = operation;
            _identifier = identifier;
            _activity = activity;
            _stopwatch = stopwatch;
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _stopwatch.Stop();
            
            if (_logger.IsEnabled(LogLevel.Information))
            {
                var message = _identifier != null
                    ? "[{Operation}] ⏱️ Completed | {Identifier} | {Duration}ms"
                    : "[{Operation}] ⏱️ Completed | {Duration}ms";
                
                _logger.LogInformation(message, _operation, _identifier, _stopwatch.ElapsedMilliseconds);
            }

            _activity?.Dispose();
            _disposed = true;
        }
    }
}