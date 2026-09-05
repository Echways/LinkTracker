using System.Text.Json;
using LinkTracker.Scrapper.Application.Abstractions.Cache;
using LinkTracker.Scrapper.Contracts.Responses;
using LinkTracker.Scrapper.Infrastructure.Cache.Helpers;
using LinkTracker.Scrapper.Infrastructure.Configuration.Valkey;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.Scrapper.Infrastructure.Cache.Implementation;

internal sealed class ValkeyLinksResponseCache(
    IKeyValueCache keyValueCache,
    LinksResponseCacheKeyBuilder keyBuilder,
    IOptions<ValkeyOptions> options,
    ILogger<ValkeyLinksResponseCache> logger) : ILinksResponseCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ValkeyOptions _options = options.Value;

    public async Task<ListLinksResponse?> GetAsync(long chatId, CancellationToken ct = default)
    {
        var key = BuildKey(chatId);

        var value = await keyValueCache.GetStringAsync(key, ct);

        if (string.IsNullOrWhiteSpace(value))
        {
            logger.LogDebug(
                "Valkey cache MISS. ChatId={ChatId}, Key={Key}",
                chatId,
                key);

            return null;
        }

        logger.LogDebug(
            "Valkey cache HIT. ChatId={ChatId}, Key={Key}",
            chatId,
            key);

        try
        {
            return JsonSerializer.Deserialize<ListLinksResponse>(value, SerializerOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Failed to deserialize links response from Valkey cache. ChatId={ChatId}, Key={Key}",
                chatId,
                key);

            await keyValueCache.DeleteAsync(key, ct);
            return null;
        }
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

        var response = await factory(ct);

        await SetAsync(chatId, response, ct);

        return response;
    }

    public async Task SetAsync(long chatId, ListLinksResponse response, CancellationToken ct = default)
    {
        var key = BuildKey(chatId);
        var value = JsonSerializer.Serialize(response, SerializerOptions);
        var ttl = TimeSpan.FromSeconds(_options.LinksTtlSeconds);

        await keyValueCache.SetStringAsync(key, value, ttl, ct);

        logger.LogDebug(
            "Valkey cache SET. ChatId={ChatId}, Key={Key}, TtlSeconds={TtlSeconds}",
            chatId,
            key,
            ttl.TotalSeconds);
    }

    public async Task InvalidateAsync(long chatId, CancellationToken ct = default)
    {
        var key = BuildKey(chatId);

        await keyValueCache.DeleteAsync(key, ct);

        logger.LogDebug(
            "Valkey cache INVALIDATE. ChatId={ChatId}, Key={Key}",
            chatId,
            key);
    }

    private string BuildKey(long chatId)
    {
        return keyBuilder.Build(chatId);
    }
}