using LinkTracker.Scrapper.Application.Abstractions.Updates;
using LinkTracker.Scrapper.Application.Models.Updates;
using LinkTracker.Scrapper.Application.Services.Helpers;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace LinkTracker.Scrapper.Application.Services.Updates.Clients;

public abstract class LinkUpdateHandlerBase(ILogger logger) : ILinkUpdateHandler
{
    protected ILogger Logger { get; } = logger;

    public abstract bool CanHandle(Uri url);

    public async Task<LinkCheckResult> CheckAsync(
        TrackedLinkSubscription subscription,
        CancellationToken ct = default)
    {
        if (!CanHandle(subscription.Url))
        {
            Logger.LogDebug("Пропускаю неподдерживаемую ссылку {Url}", subscription.Url);
            return LinkUpdateResultBuilder.NoChanges();
        }

        if (subscription.LastUpdatedAt is null)
        {
            return await InitializeStateAsync(subscription, ct);
        }

        var events = await GetNewEventsAsync(
            subscription,
            subscription.LastUpdatedAt.Value,
            subscription.LastEventKey,
            ct);

        if (events.Count == 0)
        {
            return LinkUpdateResultBuilder.NoChanges(
                subscription.LastUpdatedAt,
                subscription.LastEventKey);
        }

        var ordered = events
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.EventKey, StringComparer.Ordinal)
            .ToArray();

        return LinkUpdateResultBuilder.FromEvents(ordered);
    }

    protected abstract Task<LinkCheckResult> InitializeStateAsync(
        TrackedLinkSubscription subscription, CancellationToken ct);

    protected abstract Task<IReadOnlyList<LinkEvent>> GetNewEventsAsync(
        TrackedLinkSubscription subscription,
        DateTimeOffset lastSeenAt,
        string? lastEventKey,
        CancellationToken ct);

    protected static bool IsAfterCursor(
        DateTimeOffset createdAt,
        string eventKey,
        DateTimeOffset lastUpdatedAt,
        string? lastEventKey)
    {
        if (createdAt > lastUpdatedAt)
        {
            return true;
        }

        if (createdAt < lastUpdatedAt)
        {
            return false;
        }

        return string.IsNullOrEmpty(lastEventKey)
               || string.CompareOrdinal(eventKey, lastEventKey) > 0;
    }

    protected static bool IsAfterCursor(
        LinkEvent linkEvent,
        DateTimeOffset lastUpdatedAt,
        string? lastEventKey)
    {
        return IsAfterCursor(linkEvent.CreatedAt, linkEvent.EventKey, lastUpdatedAt, lastEventKey);
    }
}