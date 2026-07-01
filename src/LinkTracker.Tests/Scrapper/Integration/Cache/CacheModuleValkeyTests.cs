using LinkTracker.Scrapper.Application.Abstractions.Cache;
using LinkTracker.Scrapper.Contracts.Responses;
using LinkTracker.Scrapper.Infrastructure.Cache.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Cache.Implementation;
using LinkTracker.Scrapper.Infrastructure.Cache.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Tests.Scrapper.Integration.Cache;

[Trait("Module", "Scrapper")]
[Trait("Category", "Integration")]
[Collection("Valkey collection")]
public sealed class CacheModuleValkeyTests(ValkeyTestContainerFixture fixture)
{
    [Fact]
    public async Task AddCache_WhenValkeyEnabled_ResolvesValkeyImplementationsAndCacheIsUsable()
    {
        await fixture.ResetAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Valkey:Enabled"] = "true",
                ["Valkey:ClientSideCachingEnabled"] = "false",
                ["Valkey:ConnectionString"] = fixture.ConnectionString,
                ["Valkey:InstanceName"] = "linktracker-module-tests",
                ["Valkey:LinksTtlSeconds"] = "60"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddCache(configuration);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<ValkeyConnectionProvider>(
            serviceProvider.GetRequiredService<IValkeyConnectionProvider>());
        Assert.IsType<ValkeyKeyValueCache>(
            serviceProvider.GetRequiredService<IKeyValueCache>());

        var cache = Assert.IsType<ValkeyLinksResponseCache>(
            serviceProvider.GetRequiredService<ILinksResponseCache>());

        const long chatId = 2001;
        var response = new ListLinksResponse
        {
            Size = 1,
            Links =
            [
                new LinkResponse { Id = 42, Url = new Uri("https://github.com/user/repo"), Tags = ["backend"], Filters = [] }
            ]
        };

        await cache.SetAsync(chatId, response, CancellationToken.None);

        var cached = await cache.GetAsync(chatId, CancellationToken.None);

        Assert.NotNull(cached);

        var link = Assert.Single(cached.Links);

        Assert.Equal(42, link.Id);
        Assert.Equal(new Uri("https://github.com/user/repo"), link.Url);
        Assert.Equal(["backend"], link.Tags);
    }

    [Fact]
    public void AddCache_WhenValkeyDisabled_ResolvesNullLinksResponseCache()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Valkey:Enabled"] = "false", ["Valkey:ClientSideCachingEnabled"] = "false" })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddCache(configuration);

        using var serviceProvider = services.BuildServiceProvider();

        var cache = serviceProvider.GetRequiredService<ILinksResponseCache>();

        Assert.IsType<NullLinksResponseCache>(cache);
    }
}