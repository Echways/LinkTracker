using LinkTracker.Scrapper.Application.Abstractions.Cache;
using LinkTracker.Scrapper.Contracts.Responses;

namespace LinkTracker.Scrapper.Infrastructure.Cache.Implementation;

public sealed class NullLinksResponseCache : ILinksResponseCache
{
    public Task<ListLinksResponse?> GetAsync(long chatId, CancellationToken ct = default)
    {
        return Task.FromResult<ListLinksResponse?>(null);
    }

    public Task<ListLinksResponse> GetOrCreateAsync(long chatId, Func<CancellationToken, Task<ListLinksResponse>> factory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory(ct);
    }

    public Task SetAsync(long chatId, ListLinksResponse response, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(long chatId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}