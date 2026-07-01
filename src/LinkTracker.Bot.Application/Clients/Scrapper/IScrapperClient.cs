using LinkTracker.Bot.Application.Clients.Scrapper.Contracts.Responses;

namespace LinkTracker.Bot.Application.Clients.Scrapper;

public interface IScrapperClient
{
    Task RegisterChatAsync(long chatId, CancellationToken ct = default);

    Task DeleteChatAsync(long chatId, CancellationToken ct = default);

    Task<ListLinksResponse> GetLinksAsync(long chatId, CancellationToken ct = default);

    Task<LinkResponse> AddLinkAsync(
        long chatId,
        Uri link,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> filters,
        CancellationToken ct = default);

    Task<LinkResponse> RemoveLinkAsync(long chatId, Uri link, CancellationToken ct = default);
}