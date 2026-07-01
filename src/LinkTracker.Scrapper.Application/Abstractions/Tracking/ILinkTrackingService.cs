using LinkTracker.Scrapper.Storage.Abstractions.Models;

namespace LinkTracker.Scrapper.Application.Abstractions.Tracking;

public interface ILinkTrackingService
{
    Task RegisterChatAsync(long chatId, CancellationToken ct = default);

    Task DeleteChatAsync(long chatId, CancellationToken ct = default);

    Task<IReadOnlyList<TrackedLinkRecord>> GetLinksAsync(long chatId, CancellationToken ct = default);

    Task<TrackedLinkRecord> AddLinkAsync(
        long chatId,
        Uri link,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> filters,
        CancellationToken ct = default);

    Task<TrackedLinkRecord> RemoveLinkAsync(long chatId, Uri link, CancellationToken ct = default);
}