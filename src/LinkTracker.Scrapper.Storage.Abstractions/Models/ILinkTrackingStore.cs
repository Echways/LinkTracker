namespace LinkTracker.Scrapper.Storage.Abstractions.Models;

public interface ILinkTrackingStore
{
    Task<bool> TryRegisterChatAsync(long chatId, CancellationToken ct = default);

    Task<bool> TryDeleteChatAsync(long chatId, CancellationToken ct = default);

    Task<bool> ChatExistsAsync(long chatId, CancellationToken ct = default);

    Task<IReadOnlyList<TrackedLinkRecord>> GetAllTrackedLinkRecordsAsync(long chatId, CancellationToken ct = default);

    Task<TrackedLinkRecord?> TryAddAsync(
        long chatId,
        Uri link,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> filters,
        CancellationToken ct = default);

    Task<TrackedLinkRecord?> TryRemoveAsync(long chatId, Uri link, CancellationToken ct = default);

    Task<bool> TryCreateTagAsync(string tag, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetTagsAsync(long chatId, CancellationToken ct = default);

    Task<TrackedLinkRecord?> TryAddTagAsync(long chatId, Uri link, string tag, CancellationToken ct = default);

    Task<bool> TryRenameTagAsync(long chatId, string tag, string newTag, CancellationToken ct = default);

    Task<bool> TryDeleteTagAsync(long chatId, string tag, CancellationToken ct = default);

    Task<IReadOnlyList<TrackedLinkSubscription>> GetAllSubscriptionsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TrackedLinkSubscription>> GetSubscriptionsBatchAsync(
        long? afterLinkId,
        int batchSize,
        CancellationToken ct = default);

    Task SetCursorAsync(
        long linkId,
        DateTimeOffset lastUpdatedAt,
        string? lastEventKey,
        CancellationToken ct = default);
}