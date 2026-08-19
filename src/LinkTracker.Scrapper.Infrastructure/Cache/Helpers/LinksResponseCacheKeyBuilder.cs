using LinkTracker.Scrapper.Infrastructure.Configuration.Valkey;
using Microsoft.Extensions.Options;

namespace LinkTracker.Scrapper.Infrastructure.Cache.Helpers;

internal sealed class LinksResponseCacheKeyBuilder(IOptions<ValkeyOptions> options)
{
    private readonly ValkeyOptions _options = options.Value;

    public string Build(long chatId)
    {
        return $"{_options.InstanceName}:links:{{chat:{chatId}}}";
    }
}
