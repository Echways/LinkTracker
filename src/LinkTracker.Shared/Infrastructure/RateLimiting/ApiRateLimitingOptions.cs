namespace LinkTracker.Shared.Infrastructure.RateLimiting;

public sealed class ApiRateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int PermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;

    public int SegmentsPerWindow { get; set; } = 6;

    public int QueueLimit { get; set; }

    public string PartitionHeaderName { get; set; } = "Tg-Chat-Id";

    public IReadOnlyList<string> TrustedNetworks { get; set; } = [];
}
