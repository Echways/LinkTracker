using StackExchange.Redis;

namespace LinkTracker.Shared.Infrastructure.Valkey;

public static class ValkeyConfiguration
{
    private static readonly ValkeyDefaultOptionsProvider Defaults = new();

    public static ConfigurationOptions Parse(string connectionString)
    {
        var configuration = ConfigurationOptions.Parse(connectionString);

        configuration.Defaults = Defaults;

        return configuration;
    }
}
