using System.Threading;

namespace ScraperBackendService.Core.Concurrency
{
    public class ProviderConcurrencyLimiter
    {
        private readonly SemaphoreSlim _sem;
        private readonly int _max;

        public ProviderConcurrencyLimiter(int maxConcurrent)
        {
            _max = maxConcurrent <= 0 ? 1 : maxConcurrent;
            _sem = new SemaphoreSlim(_max, _max);
        }

        public Task WaitAsync(CancellationToken ct) => _sem.WaitAsync(ct);

        public Task<bool> TryWaitAsync(int millisecondsTimeout, CancellationToken ct) => _sem.WaitAsync(millisecondsTimeout, ct);

        public void Release() => _sem.Release();

        public void Dispose() => _sem.Dispose();

        /// <summary>
        /// Current available slots in the limiter (for diagnostics).
        /// </summary>
        public int CurrentCount => _sem.CurrentCount;

        /// <summary>
        /// Configured maximum concurrent slots.
        /// </summary>
        public int MaxCount => _max;
    }

    public sealed class HanimeConcurrencyLimiter : ProviderConcurrencyLimiter
    {
        public HanimeConcurrencyLimiter(int maxConcurrent) : base(maxConcurrent) { }
    }

    public sealed class DlsiteConcurrencyLimiter : ProviderConcurrencyLimiter
    {
        public DlsiteConcurrencyLimiter(int maxConcurrent) : base(maxConcurrent) { }
    }
}
