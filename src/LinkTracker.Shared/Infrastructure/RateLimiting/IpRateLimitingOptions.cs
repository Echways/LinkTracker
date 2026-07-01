namespace LinkTracker.Shared.Infrastructure.RateLimiting;

public sealed class IpRateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int PermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;

    public int SegmentsPerWindow { get; set; } = 6;

    public int QueueLimit { get; set; } = 0;
}