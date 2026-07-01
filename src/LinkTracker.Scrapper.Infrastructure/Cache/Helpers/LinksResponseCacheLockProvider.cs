namespace LinkTracker.Scrapper.Infrastructure.Cache.Helpers;

internal sealed class LinksResponseCacheLockProvider
{
    private const int StripesCount = 1_024;

    private readonly SemaphoreSlim[] _locks = Enumerable
        .Range(0, StripesCount)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public async Task<IDisposable> AcquireAsync(long chatId, CancellationToken ct = default)
    {
        var semaphore = _locks[GetLockIndex(chatId)];

        await semaphore.WaitAsync(ct);

        return new SemaphoreReleaser(semaphore);
    }

    private static int GetLockIndex(long chatId)
    {
        return (int)((ulong)chatId % StripesCount);
    }

    private sealed class SemaphoreReleaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}