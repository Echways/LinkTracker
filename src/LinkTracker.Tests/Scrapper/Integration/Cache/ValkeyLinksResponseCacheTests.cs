using LinkTracker.Scrapper.Contracts.Responses;
using LinkTracker.Scrapper.Infrastructure.Cache.Helpers;
using LinkTracker.Scrapper.Infrastructure.Cache.Implementation;
using LinkTracker.Scrapper.Infrastructure.Configuration.Valkey;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkTracker.Tests.Scrapper.Integration.Cache;

[Trait("Module", "Scrapper")]
[Trait("Category", "Integration")]
[Collection("Valkey collection")]
public sealed class ValkeyLinksResponseCacheTests(ValkeyTestContainerFixture fixture)
{
    private const string InstanceName = "linktracker-tests";

    [Fact]
    public async Task SetAndGetAsync_WhenResponseCached_RoundTripsListLinksResponse()
    {
        await fixture.ResetAsync();

        var sut = CreateSut();
        const long chatId = 1001;

        var response = new ListLinksResponse
        {
            Size = 2,
            Links =
            [
                new LinkResponse { Id = 42, Url = new Uri("https://github.com/user/repo"), Tags = ["backend", "dotnet"] },
                new LinkResponse { Id = 43, Url = new Uri("https://stackoverflow.com/questions/123"), Tags = ["qa"] }
            ]
        };

        await sut.SetAsync(chatId, response, CancellationToken.None);

        var cached = await sut.GetAsync(chatId, CancellationToken.None);

        Assert.NotNull(cached);
        Assert.Equal(2, cached.Size);

        Assert.Collection(
            cached.Links,
            first =>
            {
                Assert.Equal(42, first.Id);
                Assert.Equal(new Uri("https://github.com/user/repo"), first.Url);
                Assert.Equal(["backend", "dotnet"], first.Tags);
            },
            second =>
            {
                Assert.Equal(43, second.Id);
                Assert.Equal(new Uri("https://stackoverflow.com/questions/123"), second.Url);
                Assert.Equal(["qa"], second.Tags);
            });
    }

    [Fact]
    public async Task InvalidateAsync_WhenResponseExists_RemovesCachedResponse()
    {
        await fixture.ResetAsync();

        var sut = CreateSut();
        const long chatId = 1002;

        await sut.SetAsync(
            chatId,
            new ListLinksResponse
            {
                Size = 1,
                Links =
                [
                    new LinkResponse { Id = 1, Url = new Uri("https://github.com/user/repo"), Tags = [] }
                ]
            },
            CancellationToken.None);

        Assert.NotNull(await sut.GetAsync(chatId, CancellationToken.None));

        await sut.InvalidateAsync(chatId, CancellationToken.None);

        Assert.Null(await sut.GetAsync(chatId, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_WhenCachedPayloadIsInvalid_DeletesBrokenValueAndReturnsNull()
    {
        await fixture.ResetAsync();

        const long chatId = 1003;

        var options = CreateOptions();
        var keyBuilder = new LinksResponseCacheKeyBuilder(options);
        var key = keyBuilder.Build(chatId);

        var keyValueCache = CreateKeyValueCache();
        var sut = CreateSut(keyValueCache, options);

        await keyValueCache.SetStringAsync(
            key,
            "{ invalid json",
            TimeSpan.FromMinutes(1),
            CancellationToken.None);

        var response = await sut.GetAsync(chatId, CancellationToken.None);

        Assert.Null(response);

        var cachedValue = await keyValueCache.GetStringAsync(key, CancellationToken.None);

        Assert.Null(cachedValue);
    }

    [Fact]
    public async Task SetAsync_WhenConfiguredTtlExpires_RemovesCachedResponse()
    {
        await fixture.ResetAsync();

        var sut = CreateSut(1);
        const long chatId = 1004;

        await sut.SetAsync(
            chatId,
            new ListLinksResponse
            {
                Size = 1,
                Links =
                [
                    new LinkResponse { Id = 42, Url = new Uri("https://github.com/user/repo"), Tags = ["backend"] }
                ]
            },
            CancellationToken.None);

        Assert.NotNull(await sut.GetAsync(chatId, CancellationToken.None));

        await WaitUntilAsync(async () => await sut.GetAsync(chatId, CancellationToken.None) is null);
    }

    private ValkeyLinksResponseCache CreateSut(int linksTtlSeconds = 60)
    {
        var options = CreateOptions(linksTtlSeconds);

        return CreateSut(CreateKeyValueCache(), options);
    }

    private static ValkeyLinksResponseCache CreateSut(
        ValkeyKeyValueCache keyValueCache,
        IOptions<ValkeyOptions> options)
    {
        return new ValkeyLinksResponseCache(
            keyValueCache,
            new LinksResponseCacheKeyBuilder(options),
            options,
            NullLogger<ValkeyLinksResponseCache>.Instance);
    }

    private ValkeyKeyValueCache CreateKeyValueCache()
    {
        var provider = new ValkeyConnectionProvider(
            Options.Create(new ValkeyOptions
            {
                Enabled = true,
                ConnectionString = fixture.ConnectionString,
                InstanceName = InstanceName,
                LinksTtlSeconds = 60,
                ClientSideCachingEnabled = false,
                ClientSideCacheTtlSeconds = 10,
                ClientSideCacheMaxEntries = 1_000
            }),
            NullLogger<ValkeyConnectionProvider>.Instance);

        return new ValkeyKeyValueCache(
            provider,
            NullLogger<ValkeyKeyValueCache>.Instance);
    }

    private static IOptions<ValkeyOptions> CreateOptions(int linksTtlSeconds = 60)
    {
        return Options.Create(new ValkeyOptions
        {
            Enabled = true,
            ConnectionString = "localhost:6379",
            InstanceName = InstanceName,
            LinksTtlSeconds = linksTtlSeconds,
            ClientSideCachingEnabled = false,
            ClientSideCacheTtlSeconds = 10,
            ClientSideCacheMaxEntries = 1_000
        });
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        const int maxAttempts = 20;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        Assert.True(await condition(), "Condition was not satisfied in time.");
    }
}