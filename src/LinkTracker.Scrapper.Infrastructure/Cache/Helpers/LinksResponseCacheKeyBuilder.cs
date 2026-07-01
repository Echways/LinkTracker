using LinkTracker.Scrapper.Infrastructure.Configuration.Valkey;
using Microsoft.Extensions.Options;

namespace LinkTracker.Scrapper.Infrastructure.Cache.Helpers;

internal sealed class LinksResponseCacheKeyBuilder(IOptions<ValkeyOptions> options)
{
    private const string LinksCacheHashTag = "linktracker-links";

    private readonly ValkeyOptions _options = options.Value;

    public string Build(long chatId)
    {
        return $"{BuildPrefix()}{chatId}";
    }

    public string BuildPrefix()
    {
        return $"{_options.InstanceName}:{{{LinksCacheHashTag}}}:links:chat:";
    }
}