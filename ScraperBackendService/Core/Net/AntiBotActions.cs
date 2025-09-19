using Microsoft.Playwright;

namespace ScraperBackendService.Core.Net
{
    /// <summary>
    /// Lightweight humanized anti-bot actions: random movement, hovering, scrolling, occasional key presses.
    /// Called as hooks in PlaywrightNetworkClient / ContextManagerNetworkClient.
    /// </summary>
    public static class AntiBotActions
    {
        private static readonly Random _rnd = new();

        /// <summary>
        /// Execute a set of "humanized" light operations on the current page.
        /// For safety, internally swallows all exceptions except cancellation.
        /// </summary>
        public static async Task HumanLikeAsync(IPage page, CancellationToken ct)
        {
            try
            {
                await DelayAsync(600, 1400, ct);

                int vw = page.ViewportSize?.Width ?? 1280;
                int vh = page.ViewportSize?.Height ?? 900;

                int x0 = _rnd.Next(60, Math.Max(100, Math.Min(600, vw / 2)));
                int y0 = _rnd.Next(60, Math.Max(100, Math.Min(500, vh / 2)));
                int x1 = _rnd.Next(vw / 4, vw * 3 / 4);
                int y1 = _rnd.Next(vh / 4, vh * 3 / 4);

                await MoveAsync(page.Mouse, x0, y0, x1, y1, _rnd.Next(6, 16), ct);

                // Randomly hover over some elements
                var nodes = await page.QuerySelectorAllAsync("a, img, button, div[role='button']");
                int hoverTimes = _rnd.Next(0, Math.Min(3, nodes.Count));
                for (int i = 0; i < hoverTimes; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var node = nodes[_rnd.Next(nodes.Count)];
                    var box = await node.BoundingBoxAsync();
                    if (box is null) continue;

                    int hx = (int)(box.X + box.Width / 2);
                    int hy = (int)(box.Y + box.Height / 2);

                    await MoveAsync(page.Mouse, x1, y1, hx, hy, _rnd.Next(6, 12), ct);
                    await node.HoverAsync();
                    await DelayAsync(500, 1400, ct);

                    x1 = hx; y1 = hy;
                }

                // Light scrolling
                if (_rnd.NextDouble() < 0.9)
                {
                    int total = _rnd.Next(150, 1000);
                    int chunk = _rnd.Next(80, 220);
                    int scrolled = 0;
                    while (scrolled < total)
                    {
                        ct.ThrowIfCancellationRequested();
                        int delta = Math.Min(chunk, total - scrolled);
                        await page.Mouse.WheelAsync(0, delta);
                        scrolled += delta;
                        await DelayAsync(180, 450, ct);
                    }
                }

                // Occasional key press
                if (_rnd.NextDouble() < 0.35)
                {
                    await page.Keyboard.PressAsync(_rnd.NextDouble() < 0.5 ? "PageDown" : "ArrowDown");
                    await DelayAsync(200, 700, ct);
                }

                // End by moving to corner
                int ex = _rnd.Next(20, Math.Min(200, vw - 20));
                int ey = _rnd.Next(20, Math.Min(200, vh - 20));
                await MoveAsync(page.Mouse, x1, y1, ex, ey, _rnd.Next(8, 18), ct);

                await DelayAsync(700, 1600, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* Ignore any anti-bot action failures */ }
        }

        private static async Task DelayAsync(int minMs, int maxMs, CancellationToken ct)
        {
            await Task.Delay(_rnd.Next(minMs, maxMs), ct);
        }

        private static async Task MoveAsync(IMouse mouse, int x0, int y0, int x1, int y1, int steps, CancellationToken ct)
        {
            float dx = (x1 - x0) / (float)steps;
            float dy = (y1 - y0) / (float)steps;
            for (int i = 1; i <= steps; i++)
            {
                ct.ThrowIfCancellationRequested();
                await mouse.MoveAsync(x0 + dx * i, y0 + dy * i, new MouseMoveOptions { Steps = 1 });
                await Task.Delay(_rnd.Next(15, 70), ct);
            }
        }
    }
}
