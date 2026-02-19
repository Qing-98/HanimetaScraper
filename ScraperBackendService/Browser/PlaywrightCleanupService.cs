using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScraperBackendService.Browser;
using ScraperBackendService.Core.Logging;

namespace ScraperBackendService.Browser;

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
        _logger.LogDebug("PlaywrightCleanup", "Playwright cleanup service started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("PlaywrightCleanup", "Stopping Playwright cleanup service...");

        try
        {
            await _contextManager.DisposeAsync().ConfigureAwait(false);
            await _playwrightService.DisposeAsync().ConfigureAwait(false);
            _logger.LogSuccess("PlaywrightCleanup", "Playwright resources cleaned up successfully");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("PlaywrightCleanup", "Error during Playwright cleanup", null, ex);
        }
    }
}
