using LinkTracker.Scrapper.Infrastructure.Cache.Implementation;
using LinkTracker.Scrapper.Infrastructure.Configuration.Valkey;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkTracker.Tests.Scrapper.Integration.Cache;

[Trait("Module", "Scrapper")]
[Trait("Category", "Integration")]
[Collection("Valkey collection")]
public sealed class ValkeyKeyValueCacheTests(ValkeyTestContainerFixture fixture)
{
    [Fact]
    public async Task SetGetAndDeleteAsync_WhenValkeyIsAvailable_RoundTripsStringValue()
    {
        await fixture.ResetAsync();

        var sut = CreateSut();
        var key = $"linktracker:test:key-value:{Guid.NewGuid():N}";

        await sut.SetStringAsync(key, "cached value", TimeSpan.FromMinutes(5), CancellationToken.None);

        var cached = await sut.GetStringAsync(key, CancellationToken.None);

        Assert.Equal("cached value", cached);

        await sut.DeleteAsync(key, CancellationToken.None);

        var deleted = await sut.GetStringAsync(key, CancellationToken.None);

        Assert.Null(deleted);
    }

    [Fact]
    public async Task SetStringAsync_WhenTtlExpires_RemovesCachedValue()
    {
        await fixture.ResetAsync();

        var sut = CreateSut();
        var key = $"linktracker:test:key-value-ttl:{Guid.NewGuid():N}";

        await sut.SetStringAsync(key, "temporary value", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal("temporary value", await sut.GetStringAsync(key, CancellationToken.None));

        await WaitUntilAsync(async () => await sut.GetStringAsync(key, CancellationToken.None) is null);
    }

    private ValkeyKeyValueCache CreateSut()
    {
        var provider = new ValkeyConnectionProvider(
            Options.Create(new ValkeyOptions { ConnectionString = fixture.ConnectionString }),
            NullLogger<ValkeyConnectionProvider>.Instance);

        return new ValkeyKeyValueCache(
            provider,
            NullLogger<ValkeyKeyValueCache>.Instance);
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