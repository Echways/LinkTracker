namespace LinkTracker.Bot.Infrastructure.Configuration.Valkey;

public sealed class DialogStateStoreOptions
{
    public const string SectionName = "Valkey";

    public bool Enabled { get; init; } = true;

    public string ConnectionString { get; init; } = string.Empty;

    public string InstanceName { get; init; } = "linktracker";

    public int DialogTtlSeconds { get; init; } = 3600;
}
