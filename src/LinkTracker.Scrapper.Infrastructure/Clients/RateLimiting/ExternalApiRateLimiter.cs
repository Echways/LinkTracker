using System.Threading.RateLimiting;
using LinkTracker.Scrapper.Infrastructure.Configuration.Clients;

namespace LinkTracker.Scrapper.Infrastructure.Clients.RateLimiting;

public sealed class ExternalApiRateLimiter : IDisposable
{
    private readonly TokenBucketRateLimiter _limiter;
    private readonly TimeProvider _timeProvider;

    private long _cooldownUntilTicks;

    public ExternalApiRateLimiter(string apiName, ExternalApiRateLimitOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        ApiName = apiName;
        AcquireTimeout = TimeSpan.FromSeconds(options.AcquireTimeoutSeconds);

        _timeProvider = timeProvider;
        _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = options.TokenLimit,
            TokensPerPeriod = options.TokensPerPeriod,
            ReplenishmentPeriod = TimeSpan.FromSeconds(options.ReplenishmentPeriodSeconds),
            QueueLimit = options.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }

    public string ApiName { get; }

    public TimeSpan AcquireTimeout { get; }

    public TimeSpan RemainingCooldown
    {
        get
        {
            var until = new DateTimeOffset(Interlocked.Read(ref _cooldownUntilTicks), TimeSpan.Zero);
            var remaining = until - _timeProvider.GetUtcNow();

            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken ct)
    {
        return _limiter.AcquireAsync(1, ct);
    }

    public void Cooldown(DateTimeOffset until)
    {
        var ticks = until.UtcTicks;

        long current;

        do
        {
            current = Interlocked.Read(ref _cooldownUntilTicks);

            if (ticks <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _cooldownUntilTicks, ticks, current) != current);
    }

    public void Dispose()
    {
        _limiter.Dispose();
    }
}
