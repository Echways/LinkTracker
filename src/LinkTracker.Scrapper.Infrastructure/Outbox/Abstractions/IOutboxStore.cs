using LinkTracker.Scrapper.Infrastructure.Outbox.Models;
using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Scrapper.Infrastructure.Outbox.Abstractions;

internal interface IOutboxStore
{
    Task AddAsync(LinkUpdate update, CancellationToken ct);

    Task AddRangeAndSetCursorAsync(
        long linkId,
        DateTimeOffset? lastUpdatedAt,
        string? lastEventKey,
        IReadOnlyCollection<LinkUpdate> updates,
        CancellationToken ct);

    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(
        int batchSize,
        int maxRetryCount,
        CancellationToken ct);

    Task MarkProcessedAsync(long id, CancellationToken ct);

    Task MarkFailedAsync(long id, string error, CancellationToken ct);
}