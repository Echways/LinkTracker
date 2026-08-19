namespace LinkTracker.Scrapper.Infrastructure.Configuration.Clients;

public sealed class ExternalApiRateLimitOptions
{
    public bool Enabled { get; init; } = true;

    public int TokenLimit { get; init; } = 100;

    public int TokensPerPeriod { get; init; } = 80;

    public int ReplenishmentPeriodSeconds { get; init; } = 60;

    public int QueueLimit { get; init; } = 1_000;

    public int AcquireTimeoutSeconds { get; init; } = 60;
}
