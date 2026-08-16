namespace LinkTracker.Scrapper.Storage.Abstractions.Models;

public sealed class TrackedLinkRecord
{
    public long Id { get; init; }

    public Uri Url { get; init; } = default!;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public DateTimeOffset? LastUpdatedAt { get; set; }

    public string? LastEventKey { get; set; }
}