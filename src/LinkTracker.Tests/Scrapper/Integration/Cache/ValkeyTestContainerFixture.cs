using Docker.DotNet;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace LinkTracker.Tests.Scrapper.Integration.Cache;

public sealed class ValkeyTestContainerFixture : IAsyncLifetime
{
    private const string ValkeyImage = "valkey/valkey:9.0.3-alpine";

    private readonly RedisContainer _container = new RedisBuilder(ValkeyImage)
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await _container.StartAsync();
                break;
            }
            catch (DockerApiException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
            }
        }

        ConnectionString = $"{_container.GetConnectionString()},abortConnect=false,allowAdmin=true";

        await WaitUntilValkeyReadyAsync(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        var configuration = ConfigurationOptions.Parse(ConnectionString);
        configuration.AllowAdmin = true;

        await using var connection = await ConnectionMultiplexer.ConnectAsync(configuration);
        var endpoint = connection.GetEndPoints().Single();
        var server = connection.GetServer(endpoint);

        await server.FlushDatabaseAsync();
    }

    public async Task<string?> GetStringAsync(string key)
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
        var database = connection.GetDatabase();

        var value = await database.StringGetAsync(key);

        return value.IsNull ? null : value.ToString();
    }

    private static async Task WaitUntilValkeyReadyAsync(string connectionString)
    {
        const int maxAttempts = 20;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
                var database = connection.GetDatabase();

                if (await database.PingAsync() >= TimeSpan.Zero)
                {
                    return;
                }
            }
            catch (RedisException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
            }
            catch (TimeoutException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
            }
        }

        throw new InvalidOperationException("Valkey did not become ready in time.");
    }
}