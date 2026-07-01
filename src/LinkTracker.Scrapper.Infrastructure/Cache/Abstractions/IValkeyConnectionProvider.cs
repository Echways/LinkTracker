using StackExchange.Redis;

namespace LinkTracker.Scrapper.Infrastructure.Cache.Abstractions;

public interface IValkeyConnectionProvider
{
    Task<IConnectionMultiplexer> GetConnectionAsync(CancellationToken ct = default);
}