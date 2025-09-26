using System;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HanimeScraper.Extensions;

/// <summary>
/// Extension methods for logging with configuration-based control.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Logs a debug message if logging is enabled in configuration.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    public static void LogDebugIfEnabled(this ILogger logger, string message)
    {
        if (Configuration.ConfigurationManager.IsLoggingEnabled())
        {
            logger.LogDebug("{Message}", message);
        }
    }

    /// <summary>
    /// Logs an information message if logging is enabled in configuration.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    public static void LogInformationIfEnabled(this ILogger logger, string message)
    {
        if (Configuration.ConfigurationManager.IsLoggingEnabled())
        {
            logger.LogInformation("{Message}", message);
        }
    }

    /// <summary>
    /// Logs a warning message if logging is enabled in configuration.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    public static void LogWarningIfEnabled(this ILogger logger, string message)
    {
        if (Configuration.ConfigurationManager.IsLoggingEnabled())
        {
            logger.LogWarning("{Message}", message);
        }
    }

    /// <summary>
    /// Logs an error message if logging is enabled in configuration.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception to log.</param>
    public static void LogErrorIfEnabled(this ILogger logger, string message, Exception? exception = null)
    {
        if (Configuration.ConfigurationManager.IsLoggingEnabled())
        {
            logger.LogError(exception, "{Message}", message);
        }
    }
}
