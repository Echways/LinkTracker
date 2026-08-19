namespace LinkTracker.Scrapper.Infrastructure.Quartz.Configuration;

public sealed class LinkUpdatesSchedulingOptions
{
    public int IntervalSeconds { get; init; } = 300;
    public int BatchSize { get; init; } = 100;
    public int MaxDegreeOfParallelism { get; init; } = 4;
}
