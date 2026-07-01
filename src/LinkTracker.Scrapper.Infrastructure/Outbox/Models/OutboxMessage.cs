using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Scrapper.Infrastructure.Outbox.Models;

internal sealed class OutboxMessage
{
    public long Id { get; init; }

    public LinkUpdate Payload { get; init; } = default!;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; init; }

    public string? Error { get; init; }

    public int RetryCount { get; init; }
}