using LinkTracker.Scrapper.Application.Abstractions.Tracking;
using LinkTracker.Scrapper.Application.Abstractions.Updates;
using LinkTracker.Scrapper.Application.Errors;
using LinkTracker.Scrapper.Storage.Abstractions.Models;

namespace LinkTracker.Scrapper.Application.Services.Tracking;

public sealed class LinkTrackingService(
    ILinkTrackingStore store,
    IEnumerable<ILinkUpdateHandler> linkUpdateHandlers) : ILinkTrackingService
{
    public async Task RegisterChatAsync(long chatId, CancellationToken ct = default)
    {
        ValidateChatId(chatId);

        if (!await store.TryRegisterChatAsync(chatId, ct))
        {
            throw ScrapperErrors.ChatAlreadyExists(chatId);
        }
    }

    public async Task DeleteChatAsync(long chatId, CancellationToken ct = default)
    {
        ValidateChatId(chatId);

        if (!await store.TryDeleteChatAsync(chatId, ct))
        {
            throw ScrapperErrors.ChatNotFound(chatId);
        }
    }

    public async Task<IReadOnlyList<TrackedLinkRecord>> GetLinksAsync(long chatId, CancellationToken ct = default)
    {
        ValidateChatId(chatId);

        if (!await store.ChatExistsAsync(chatId, ct))
        {
            throw ScrapperErrors.ChatNotFound(chatId);
        }

        return await store.GetAllTrackedLinkRecordsAsync(chatId, ct);
    }

    public async Task<TrackedLinkRecord> AddLinkAsync(
        long chatId,
        Uri link,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> filters,
        CancellationToken ct = default)
    {
        ValidateChatId(chatId);
        ValidateLink(link);
        ValidateSupportedLink(link);

        if (!await store.ChatExistsAsync(chatId, ct))
        {
            throw ScrapperErrors.ChatNotFound(chatId);
        }

        var record = await store.TryAddAsync(chatId, link, tags, [], ct);

        return record ?? throw ScrapperErrors.LinkAlreadyExists(link);
    }

    public async Task<TrackedLinkRecord> RemoveLinkAsync(long chatId, Uri link, CancellationToken ct = default)
    {
        ValidateChatId(chatId);
        ValidateLink(link);

        if (!await store.ChatExistsAsync(chatId, ct))
        {
            throw ScrapperErrors.ChatNotFound(chatId);
        }

        var record = await store.TryRemoveAsync(chatId, link, ct);

        return record ?? throw ScrapperErrors.LinkNotFound(link);
    }

    private void ValidateSupportedLink(Uri link)
    {
        if (!linkUpdateHandlers.Any(x => x.CanHandle(link)))
        {
            throw ScrapperErrors.UnsupportedLink(link);
        }
    }

    private static void ValidateChatId(long chatId)
    {
        if (chatId <= 0)
        {
            throw ScrapperErrors.InvalidChatId();
        }
    }

    private static void ValidateLink(Uri link)
    {
        if (!link.IsAbsoluteUri)
        {
            throw ScrapperErrors.InvalidLink();
        }

        if (link.Scheme is not ("http" or "https"))
        {
            throw ScrapperErrors.InvalidLinkScheme();
        }
    }
}