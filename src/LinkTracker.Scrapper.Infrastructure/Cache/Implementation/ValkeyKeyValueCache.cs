using LinkTracker.Scrapper.Application.Abstractions.Cache;
using LinkTracker.Scrapper.Infrastructure.Cache.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LinkTracker.Scrapper.Infrastructure.Cache.Implementation;

public sealed class ValkeyKeyValueCache(
    IValkeyConnectionProvider connectionProvider,
    ILogger<ValkeyKeyValueCache> logger) : IKeyValueCache
{
    public async Task<string?> GetStringAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var database = await GetDatabaseAsync(ct);
            var value = await database.StringGetAsync(key);

            return value.IsNull ? null : value.ToString();
        }
        catch (Exception ex) when (IsRecoverableValkeyException(ex))
        {
            logger.LogWarning(
                ex,
                "Ошибка при чтении значения из Valkey. Key={Key}",
                key);

            return null;
        }
    }

    public async Task SetStringAsync(
        string key,
        string value,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        try
        {
            var database = await GetDatabaseAsync(ct);
            await database.StringSetAsync(key, value, ttl);
        }
        catch (Exception ex) when (IsRecoverableValkeyException(ex))
        {
            logger.LogWarning(
                ex,
                "Ошибка при записи значения в Valkey. Key={Key}",
                key);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var database = await GetDatabaseAsync(ct);
            await database.KeyDeleteAsync(key);
        }
        catch (Exception ex) when (IsRecoverableValkeyException(ex))
        {
            logger.LogWarning(
                ex,
                "Ошибка при удалении значения из Valkey. Key={Key}",
                key);
        }
    }

    private async Task<IDatabase> GetDatabaseAsync(CancellationToken ct)
    {
        var connection = await connectionProvider.GetConnectionAsync(ct);
        return connection.GetDatabase();
    }

    private static bool IsRecoverableValkeyException(Exception ex)
    {
        return ex is RedisException
            or TimeoutException
            or InvalidOperationException;
    }
}