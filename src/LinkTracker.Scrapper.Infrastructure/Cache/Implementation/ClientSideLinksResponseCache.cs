using LinkTracker.Scrapper.Application.Abstractions.Cache;
using LinkTracker.Scrapper.Contracts.Responses;
using LinkTracker.Scrapper.Infrastructure.Cache.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Cache.Helpers;
using LinkTracker.Scrapper.Infrastructure.Configuration.Valkey;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.Scrapper.Infrastructure.Cache.Implementation;

internal sealed class ClientSideLinksResponseCache(
    ILinksResponseCache distributedCache,
    ILinksResponseLocalCache localCache,
    LinksResponseCacheKeyBuilder keyBuilder,
    LinksResponseCacheLockProvider lockProvider,
    IOptions<ValkeyOptions> options,
    ILogger<ClientSideLinksResponseCache> logger) : ILinksResponseCache
{
    private readonly ValkeyOptions _options = options.Value;

    public async Task<ListLinksResponse?> GetAsync(long chatId, CancellationToken ct = default)
    {
        var key = keyBuilder.Build(chatId);

        if (localCache.TryGet(key, out var localResponse))
        {
            logger.LogDebug(
                "Client-side кэш HIT. ChatId={ChatId}, Key={Key}",
                chatId,
                key);

            return localResponse;
        }

        logger.LogDebug(
            "Client-side кэш MISS. ChatId={ChatId}, Key={Key}",
            chatId,
            key);

        var distributedResponse = await distributedCache.GetAsync(chatId, ct);

        if (distributedResponse is not null)
        {
            localCache.Set(key, distributedResponse, GetLocalTtl());
        }

        return distributedResponse;
    }

    public async Task<ListLinksResponse> GetOrCreateAsync(
        long chatId,
        Func<CancellationToken, Task<ListLinksResponse>> factory,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var cachedResponse = await GetAsync(chatId, ct);

        if (cachedResponse is not null)
        {
            return cachedResponse;
        }

        using var cacheLock = await lockProvider.AcquireAsync(chatId, ct);

        cachedResponse = await GetAsync(chatId, ct);

        if (cachedResponse is not null)
        {
            return cachedResponse;
        }

        var response = await factory(ct);

        await SetCoreAsync(chatId, response, ct);

        return response;
    }

    public async Task SetAsync(long chatId, ListLinksResponse response, CancellationToken ct = default)
    {
        using var cacheLock = await lockProvider.AcquireAsync(chatId, ct);

        await SetCoreAsync(chatId, response, ct);
    }

    public async Task InvalidateAsync(long chatId, CancellationToken ct = default)
    {
        using var cacheLock = await lockProvider.AcquireAsync(chatId, ct);

        await InvalidateCoreAsync(chatId, ct);
    }

    private async Task SetCoreAsync(long chatId, ListLinksResponse response, CancellationToken ct)
    {
        await distributedCache.SetAsync(chatId, response, ct);

        var key = keyBuilder.Build(chatId);
        var ttl = GetLocalTtl();

        localCache.Set(key, response, ttl);

        logger.LogDebug(
            "Client-side кэш SET. ChatId={ChatId}, Key={Key}, TtlSeconds={TtlSeconds}",
            chatId,
            key,
            ttl.TotalSeconds);
    }

    private async Task InvalidateCoreAsync(long chatId, CancellationToken ct)
    {
        var key = keyBuilder.Build(chatId);

        localCache.Remove(key);
        await distributedCache.InvalidateAsync(chatId, ct);

        logger.LogDebug(
            "Client-side кэш INVALIDATE. ChatId={ChatId}, Key={Key}",
            chatId,
            key);
    }

    private TimeSpan GetLocalTtl()
    {
        var ttlSeconds = Math.Min(
            _options.LinksTtlSeconds,
            _options.ClientSideCacheTtlSeconds);

        return TimeSpan.FromSeconds(ttlSeconds);
    }
}