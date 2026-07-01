using LinkTracker.Scrapper.Contracts.Responses;

namespace LinkTracker.Scrapper.Application.Abstractions.Cache;

public interface ILinksResponseCache
{
    Task<ListLinksResponse?> GetAsync(long chatId, CancellationToken ct = default);

    Task<ListLinksResponse> GetOrCreateAsync(
        long chatId,
        Func<CancellationToken, Task<ListLinksResponse>> factory,
        CancellationToken ct = default);

    Task SetAsync(long chatId, ListLinksResponse response, CancellationToken ct = default);

    Task InvalidateAsync(long chatId, CancellationToken ct = default);
}