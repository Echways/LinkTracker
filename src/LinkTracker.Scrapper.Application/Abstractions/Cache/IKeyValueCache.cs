namespace LinkTracker.Scrapper.Application.Abstractions.Cache;

public interface IKeyValueCache
{
    Task<string?> GetStringAsync(string key, CancellationToken ct = default);

    Task SetStringAsync(
        string key,
        string value,
        TimeSpan ttl,
        CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}