namespace LinkTracker.Scrapper.Infrastructure.Outbox.Configuration;

public sealed class OutboxOptions
{
    public bool Enabled { get; set; }

    public int DispatchIntervalSeconds { get; set; } = 10;

    public int BatchSize { get; set; } = 100;

    public int MaxRetryCount { get; set; } = 3;

    public int LockSeconds { get; set; } = 60;
}