using LinkTracker.Scrapper.Application.Models.Updates;

namespace LinkTracker.Scrapper.Application.Services.Helpers;

internal static class LinkUpdateResultBuilder
{
    public static LinkCheckResult InitialState(DateTimeOffset actualLastUpdatedAt)
    {
        return Build(actualLastUpdatedAt, null, []);
    }

    public static LinkCheckResult NoChanges(
        DateTimeOffset? actualLastUpdatedAt = null,
        string? actualLastEventKey = null)
    {
        return Build(actualLastUpdatedAt, actualLastEventKey, []);
    }

    public static LinkCheckResult FromEvent(LinkEvent linkEvent)
    {
        return Build(linkEvent.CreatedAt, linkEvent.EventKey, [linkEvent]);
    }

    public static LinkCheckResult FromEvents(IReadOnlyList<LinkEvent> events)
    {
        var lastEvent = events[^1];
        return Build(lastEvent.CreatedAt, lastEvent.EventKey, events);
    }

    private static LinkCheckResult Build(
        DateTimeOffset? actualLastUpdatedAt,
        string? actualLastEventKey,
        IReadOnlyList<LinkEvent> events)
    {
        return new LinkCheckResult { NewLastUpdatedAt = actualLastUpdatedAt, NewLastEventKey = actualLastEventKey, Events = events };
    }
}