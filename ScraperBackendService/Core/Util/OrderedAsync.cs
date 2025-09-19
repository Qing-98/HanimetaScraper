using System.Collections.Concurrent;

namespace ScraperBackendService.Core.Util;

public static class OrderedAsync
{
    /// <summary>
    /// Execute tasks concurrently while maintaining result order.
    /// </summary>
    public static async Task<List<TOut>> ForEachAsync<TIn, TOut>(
        IReadOnlyList<TIn> items,
        int degree,
        Func<TIn, Task<TOut?>> taskFactory)
    {
        var results = new TOut?[items.Count];
        var indexBag = new ConcurrentQueue<int>(Enumerable.Range(0, items.Count));
        var workers = new List<Task>();

        for (int w = 0; w < degree; w++)
        {
            workers.Add(Task.Run(async () =>
            {
                while (indexBag.TryDequeue(out var idx))
                {
                    try
                    {
                        var output = await taskFactory(items[idx]);
                        results[idx] = output;
                    }
                    catch
                    {
                        results[idx] = default;
                    }
                }
            }));
        }

        await Task.WhenAll(workers);

        return results.Where(r => r is not null).Select(r => r!).ToList();
    }
}
