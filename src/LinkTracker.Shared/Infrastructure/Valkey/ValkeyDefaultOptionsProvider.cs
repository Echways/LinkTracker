using StackExchange.Redis;
using StackExchange.Redis.Configuration;

namespace LinkTracker.Shared.Infrastructure.Valkey;

public sealed class ValkeyDefaultOptionsProvider : DefaultOptionsProvider
{
    public override bool AbortOnConnectFail => false;

    public override int ConnectRetry => 10;

    public override TimeSpan? ConnectTimeout => TimeSpan.FromSeconds(15);

    public override TimeSpan SyncTimeout => TimeSpan.FromSeconds(15);

    public override TimeSpan KeepAliveInterval => TimeSpan.FromSeconds(30);

    public override bool ResolveDns => true;

    public override IReconnectRetryPolicy ReconnectRetryPolicy => new ExponentialRetry(1000);
}
