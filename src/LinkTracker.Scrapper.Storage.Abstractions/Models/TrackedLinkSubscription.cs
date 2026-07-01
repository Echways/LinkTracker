namespace LinkTracker.Scrapper.Storage.Abstractions.Models;

public sealed class TrackedLinkSubscription
{
    public long Id { get; init; }

    public Uri Url { get; init; } = default!;

    public IReadOnlyList<long> TgChatIds { get; init; } = [];

    public DateTimeOffset? LastUpdatedAt { get; init; }
    public string? LastEventKey { get; init; }
}