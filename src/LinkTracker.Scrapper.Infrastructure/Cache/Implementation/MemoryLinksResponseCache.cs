using System.Collections.Concurrent;
using LinkTracker.Scrapper.Contracts.Responses;
using LinkTracker.Scrapper.Infrastructure.Cache.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Configuration.Valkey;
using Microsoft.Extensions.Options;

namespace LinkTracker.Scrapper.Infrastructure.Cache.Implementation;

internal sealed class MemoryLinksResponseCache(IOptions<ValkeyOptions> options) : ILinksResponseLocalCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
    private readonly int _maxEntries = options.Value.ClientSideCacheMaxEntries;

    public bool TryGet(string key, out ListLinksResponse response)
    {
        response = default!;

        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.IsExpired)
        {
            _entries.TryRemove(key, out _);
            return false;
        }

        response = entry.Response;
        return true;
    }

    public void Set(string key, ListLinksResponse response, TimeSpan ttl)
    {
        RemoveExpiredEntries();
        TrimIfNeeded();

        _entries[key] = new CacheEntry(
            response,
            DateTimeOffset.UtcNow.Add(ttl),
            DateTimeOffset.UtcNow);
    }

    public void Remove(string key)
    {
        _entries.TryRemove(key, out _);
    }

    private void RemoveExpiredEntries()
    {
        foreach (var pair in _entries)
        {
            if (pair.Value.IsExpired)
            {
                _entries.TryRemove(pair.Key, out _);
            }
        }
    }

    private void TrimIfNeeded()
    {
        if (_entries.Count < _maxEntries)
        {
            return;
        }

        var entriesToRemove = _entries.Count - _maxEntries + 1;

        foreach (var key in _entries
                     .OrderBy(x => x.Value.CreatedAt)
                     .Take(entriesToRemove)
                     .Select(x => x.Key))
        {
            _entries.TryRemove(key, out _);
        }
    }

    private sealed record CacheEntry(
        ListLinksResponse Response,
        DateTimeOffset ExpiresAt,
        DateTimeOffset CreatedAt)
    {
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    }
}