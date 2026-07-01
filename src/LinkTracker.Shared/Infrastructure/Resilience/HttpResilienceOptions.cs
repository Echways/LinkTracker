namespace LinkTracker.Shared.Infrastructure.Resilience;

public sealed class HttpResilienceOptions
{
    public const string SectionName = "Resilience:Http";

    public int TimeoutMilliseconds { get; set; } = 1000;

    public HttpRetryOptions Retry { get; set; } = new();

    public HttpCircuitBreakerOptions CircuitBreaker { get; set; } = new();
}

public sealed class HttpRetryOptions
{
    public int MaxRetryAttempts { get; set; } = 2;

    public int BackoffMilliseconds { get; set; } = 200;

    public HttpRetryBackoffStrategy BackoffStrategy { get; set; } = HttpRetryBackoffStrategy.Constant;

    public int[] RetryableStatusCodes { get; set; } = [408, 429, 500, 502, 503, 504];
}

public sealed class HttpCircuitBreakerOptions
{
    public int FailureRateThreshold { get; set; } = 100;

    public int SamplingDurationSeconds { get; set; } = 10;

    public int MinimumThroughput { get; set; } = 5;

    public int WaitDurationInOpenStateMilliseconds { get; set; } = 1000;
}