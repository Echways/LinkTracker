namespace LinkTracker.Scrapper.Application.Models.Updates;

public sealed class LinkCheckResult
{
    public DateTimeOffset? NewLastUpdatedAt { get; init; }
    public string? NewLastEventKey { get; init; }

    public IReadOnlyList<LinkEvent> Events { get; init; } = [];

    public bool HasChanges => Events.Count > 0;
}