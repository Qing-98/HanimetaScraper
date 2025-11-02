using Microsoft.Playwright;
using ScraperBackendService.Core.Abstractions;

namespace ScraperBackendService.Core.Net;

/// <summary>
/// Playwright related helper extensions.
/// </summary>
public static class PlaywrightExtensions
{
    /// <summary>
    /// Safely close a playwright page using the network client if applicable.
    /// </summary>
    /// <param name="client">INetworkClient instance.</param>
    /// <param name="page">Playwright page.</param>
    public static async Task SafeClosePageAsync(this INetworkClient client, IPage? page)
    {
        if (page == null) return;
        if (client is PlaywrightNetworkClient playwrightClient)
        {
            await playwrightClient.ClosePageAsync(page).ConfigureAwait(false);
        }
        else
        {
            try { await page.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
        }
    }
}
