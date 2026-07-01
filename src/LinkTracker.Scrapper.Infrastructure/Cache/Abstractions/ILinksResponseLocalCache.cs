using LinkTracker.Scrapper.Contracts.Responses;

namespace LinkTracker.Scrapper.Infrastructure.Cache.Abstractions;

internal interface ILinksResponseLocalCache
{
    bool TryGet(string key, out ListLinksResponse response);

    void Set(string key, ListLinksResponse response, TimeSpan ttl);

    void Remove(string key);
}