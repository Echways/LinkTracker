using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Infrastructure.Configuration.Valkey;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LinkTracker.Bot.Infrastructure.Storage.Valkey;

internal sealed class ValkeyLanguageStore(
    IConnectionMultiplexer connection,
    IOptions<DialogStateStoreOptions> options,
    ILogger<ValkeyLanguageStore> logger) : ILanguageStore
{
    private readonly DialogStateStoreOptions _options = options.Value;

    public async Task<string?> GetAsync(long chatId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var key = BuildKey(chatId);

        try
        {
            var value = await connection.GetDatabase().StringGetAsync(key);

            return value.IsNullOrEmpty ? null : (string?)value;
        }
        catch (RedisException ex)
        {
            logger.LogError(
                ex,
                "Failed to read chat language, the default language is used. ChatId={ChatId}, Key={Key}",
                chatId,
                key);

            return null;
        }
    }

    public async Task SetAsync(long chatId, string languageCode, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var key = BuildKey(chatId);

        try
        {
            await connection.GetDatabase().StringSetAsync(key, languageCode);
        }
        catch (RedisException ex)
        {
            logger.LogError(
                ex,
                "Failed to save chat language. ChatId={ChatId}, Key={Key}",
                chatId,
                key);
        }
    }

    private string BuildKey(long chatId)
    {
        return $"{_options.InstanceName}:language:{{chat:{chatId}}}";
    }
}
