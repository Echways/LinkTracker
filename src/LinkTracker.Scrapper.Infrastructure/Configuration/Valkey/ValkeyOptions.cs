namespace LinkTracker.Scrapper.Infrastructure.Configuration.Valkey;

public sealed class ValkeyOptions
{
    public const string SectionName = "Valkey";

    public bool Enabled { get; init; } = true;

    public string ConnectionString { get; init; } = string.Empty;

    public string InstanceName { get; init; } = "linktracker";

    public int LinksTtlSeconds { get; init; } = 60;

    public bool ClientSideCachingEnabled { get; init; } = true;

    public int ClientSideCacheTtlSeconds { get; init; } = 10;

    public int ClientSideCacheMaxEntries { get; init; } = 1_000;
}