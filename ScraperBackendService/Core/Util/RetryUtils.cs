namespace ScraperBackendService.Core.Util;

/// <summary>
/// Unified retry strategy utilities
/// </summary>
public static class RetryUtils
{
    /// <summary>
    /// Retry strategy with exponential backoff
    /// </summary>
    public static async Task<T> RetryWithBackoffAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        double backoffMultiplier = 2.0,
        CancellationToken cancellationToken = default)
    {
        var delay = initialDelay ?? TimeSpan.FromSeconds(1);
        Exception? lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await operation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                
                if (attempt == maxRetries)
                    break;

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * backoffMultiplier);
            }
        }

        throw new AggregateException($"Operation failed after {maxRetries + 1} attempts", lastException!);
    }

    /// <summary>
    /// Simple retry strategy
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? delay = null,
        Func<Exception, bool>? shouldRetry = null)
    {
        var actualDelay = delay ?? TimeSpan.FromSeconds(1);
        Exception? lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;
                
                if (attempt == maxRetries)
                    break;

                // Check if should retry
                if (shouldRetry != null && !shouldRetry(ex))
                    break;

                if (delay.HasValue)
                    await Task.Delay(actualDelay);
            }
        }

        throw new AggregateException($"Operation failed after {maxRetries + 1} attempts", lastException!);
    }

    /// <summary>
    /// Retry strategy for void operations
    /// </summary>
    public static async Task RetryAsync(
        Func<Task> operation,
        int maxRetries = 3,
        TimeSpan? delay = null,
        Func<Exception, bool>? shouldRetry = null)
    {
        await RetryAsync(async () =>
        {
            await operation();
            return true; // Dummy return value
        }, maxRetries, delay, shouldRetry);
    }

    /// <summary>
    /// Conditional retry judgment
    /// </summary>
    public static bool ShouldRetryOnNetworkError(Exception ex)
    {
        return ex is HttpRequestException ||
               ex is TaskCanceledException ||
               ex is TimeoutException ||
               (ex.Message?.Contains("timeout", StringComparison.OrdinalIgnoreCase) ?? false) ||
               (ex.Message?.Contains("network", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>
    /// Conditional retry judgment for Playwright-related errors
    /// </summary>
    public static bool ShouldRetryOnPlaywrightError(Exception ex)
    {
        return ShouldRetryOnNetworkError(ex) ||
               (ex.Message?.Contains("Target page", StringComparison.OrdinalIgnoreCase) ?? false) ||
               (ex.Message?.Contains("Browser has been closed", StringComparison.OrdinalIgnoreCase) ?? false) ||
               (ex.Message?.Contains("Context or page has been closed", StringComparison.OrdinalIgnoreCase) ?? false);
    }
}