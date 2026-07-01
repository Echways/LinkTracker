using LinkTracker.Scrapper.Application.Abstractions.Cache;
using LinkTracker.Scrapper.Infrastructure.Cache.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Cache.Helpers;
using LinkTracker.Scrapper.Infrastructure.Cache.Implementation;
using LinkTracker.Scrapper.Infrastructure.Configuration.Valkey;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.Scrapper.Infrastructure.Cache.Registration;

public static class CacheModule
{
    public static IServiceCollection AddCache(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<ValkeyOptions>()
            .Bind(configuration.GetSection(ValkeyOptions.SectionName))
            .Validate(
                options => !options.Enabled || options.LinksTtlSeconds > 0,
                "Valkey links TTL must be positive when Valkey cache is enabled.")
            .Validate(
                options => !options.Enabled || !options.ClientSideCachingEnabled || options.ClientSideCacheTtlSeconds > 0,
                "Valkey client-side cache TTL must be positive when client-side cache is enabled.")
            .Validate(
                options => !options.Enabled || !options.ClientSideCachingEnabled || options.ClientSideCacheMaxEntries > 0,
                "Valkey client-side cache max entries must be positive when client-side cache is enabled.")
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Valkey connection string is required when Valkey cache is enabled.")
            .ValidateOnStart();

        var options = configuration
            .GetSection(ValkeyOptions.SectionName)
            .Get<ValkeyOptions>() ?? new ValkeyOptions();

        if (!options.Enabled)
        {
            services.AddSingleton<ILinksResponseCache, NullLinksResponseCache>();
            return services;
        }

        services.AddSingleton<LinksResponseCacheKeyBuilder>();
        services.AddSingleton<LinksResponseCacheLockProvider>();
        services.AddSingleton<ValkeyLinksResponseCache>();
        services.AddSingleton<IValkeyConnectionProvider, ValkeyConnectionProvider>();
        services.AddSingleton<IKeyValueCache, ValkeyKeyValueCache>();

        if (!options.ClientSideCachingEnabled)
        {
            services.AddSingleton<ILinksResponseCache>(serviceProvider =>
                serviceProvider.GetRequiredService<ValkeyLinksResponseCache>());

            return services;
        }

        services.AddSingleton<ILinksResponseLocalCache, MemoryLinksResponseCache>();
        services.AddSingleton<ILinksResponseCache>(serviceProvider => new ClientSideLinksResponseCache(
            serviceProvider.GetRequiredService<ValkeyLinksResponseCache>(),
            serviceProvider.GetRequiredService<ILinksResponseLocalCache>(),
            serviceProvider.GetRequiredService<LinksResponseCacheKeyBuilder>(),
            serviceProvider.GetRequiredService<LinksResponseCacheLockProvider>(),
            serviceProvider.GetRequiredService<IOptions<ValkeyOptions>>(),
            serviceProvider.GetRequiredService<ILogger<ClientSideLinksResponseCache>>()));

        return services;
    }
}