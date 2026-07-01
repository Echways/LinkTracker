namespace LinkTracker.Scrapper.Storage.Sql.Models;

internal sealed class TrackedLinkRow
{
    public long Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public DateTimeOffset? LastUpdatedAt { get; init; }
    public string? LastEventKey { get; init; }
    public string[] Tags { get; init; } = [];
}

internal sealed class SubscriptionRemovalRow
{
    public long SubscriptionId { get; init; }
    public long LinkId { get; init; }
    public string Url { get; init; } = string.Empty;
    public DateTimeOffset? LastUpdatedAt { get; init; }
    public string? LastEventKey { get; init; }
    public string[] Tags { get; init; } = [];
}

internal sealed class SubscriptionRow
{
    public long Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public DateTimeOffset? LastUpdatedAt { get; init; }
    public string? LastEventKey { get; init; }
    public long ChatId { get; init; }
}

internal sealed class TagRow
{
    public string Name { get; init; } = string.Empty;
}