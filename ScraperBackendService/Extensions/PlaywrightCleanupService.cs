using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScraperBackendService.AntiCloudflare;

namespace ScraperBackendService.Extensions;

/// <summary>
/// Hosted service that ensures proper cleanup of Playwright resources on application shutdown.
/// </summary>
public class PlaywrightCleanupService : IHostedService
{
    private readonly PlaywrightService _playwrightService;
    private readonly PlaywrightContextManager _contextManager;
    private readonly ILogger<PlaywrightCleanupService> _logger;

    public PlaywrightCleanupService(
        PlaywrightService playwrightService,
        PlaywrightContextManager contextManager,
        ILogger<PlaywrightCleanupService> logger)
    {
        _playwrightService = playwrightService;
        _contextManager = contextManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Playwright cleanup service started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Stopping Playwright cleanup service...");

        try
        {
            await _contextManager.DisposeAsync().ConfigureAwait(false);
            await _playwrightService.DisposeAsync().ConfigureAwait(false);
            _logger.LogInformation("Playwright resources cleaned up successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Playwright cleanup");
        }
    }
}
