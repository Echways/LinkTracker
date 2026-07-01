using LinkTracker.Scrapper.Infrastructure.Cache.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Configuration.Valkey;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LinkTracker.Scrapper.Infrastructure.Cache.Implementation;

public sealed class ValkeyConnectionProvider(
    IOptions<ValkeyOptions> options,
    ILogger<ValkeyConnectionProvider> logger) : IValkeyConnectionProvider, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private IConnectionMultiplexer? _connection;

    public void Dispose()
    {
        _connection?.Dispose();
        _semaphore.Dispose();
    }

    public async Task<IConnectionMultiplexer> GetConnectionAsync(CancellationToken ct = default)
    {
        var current = _connection;

        if (current is not null && current.IsConnected)
        {
            return current;
        }

        await _semaphore.WaitAsync(ct);

        try
        {
            current = _connection;

            if (current is not null && current.IsConnected)
            {
                return current;
            }

            current?.Dispose();

            var configuration = ConfigurationOptions.Parse(options.Value.ConnectionString);

            configuration.AbortOnConnectFail = false;
            configuration.AllowAdmin = true;
            configuration.ConnectRetry = 10;
            configuration.ConnectTimeout = 15000;
            configuration.SyncTimeout = 15000;
            configuration.AsyncTimeout = 15000;
            configuration.ResolveDns = true;
            configuration.KeepAlive = 30;
            configuration.ReconnectRetryPolicy = new ExponentialRetry(1000);

            logger.LogInformation(
                "Подключение к Valkey. Endpoints={Endpoints}",
                string.Join(", ", configuration.EndPoints));

            _connection = await ConnectionMultiplexer.ConnectAsync(configuration);

            logger.LogInformation(
                "Подключение к Valkey установлено. IsConnected={IsConnected}",
                _connection.IsConnected);

            return _connection;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}