using LinkTracker.Scrapper.Infrastructure.Outbox;
using LinkTracker.Scrapper.Infrastructure.Outbox.Serialization;
using LinkTracker.Scrapper.Infrastructure.Telemetry;
using LinkTracker.Scrapper.Storage.Sql;
using LinkTracker.Shared.Contracts.Bot;
using LinkTracker.Tests.Scrapper.Integration.Storage;

namespace LinkTracker.Tests.Scrapper.Integration.Outbox;

[Trait("Module", "Scrapper")]
[Trait("Category", "Integration")]
[Collection("Postgres collection")]
public sealed class PostgresOutboxStoreTests(PostgresSqlStorageFixture fixture)
{
    private static readonly TimeSpan DefaultLock = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task AddRangeAndSetCursorAsync_WhenLinkExists_AddsMessagesAndUpdatesCursor()
    {
        await fixture.ResetAsync();

        var trackingStore = new SqlLinkTrackingStore(fixture.DataSource);
        var sut = CreateSut();

        const long chatId = 1001;
        var url = new Uri("https://github.com/user/repo");
        var updatedAt = new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);
        const string eventKey = "issue:123";

        await trackingStore.TryRegisterChatAsync(chatId);
        var tracked = await trackingStore.TryAddAsync(chatId, url, ["backend"]);

        Assert.NotNull(tracked);

        await sut.AddRangeAndSetCursorAsync(
            tracked!.Id,
            updatedAt,
            eventKey,
            [
                new LinkUpdate { Id = tracked.Id, Url = url, Description = "Repository updated", TgChatIds = [chatId] }
            ],
            CancellationToken.None);

        var messages = await sut.ClaimUnprocessedBatchAsync(10, 3, DefaultLock, CancellationToken.None);

        var message = Assert.Single(messages);
        Assert.Equal(tracked.Id, message.Payload.Id);
        Assert.Equal(url, message.Payload.Url);
        Assert.Equal("Repository updated", message.Payload.Description);
        Assert.Equal([chatId], message.Payload.TgChatIds);

        var links = await trackingStore.GetAllTrackedLinkRecordsAsync(chatId);
        var link = Assert.Single(links);

        Assert.Equal(updatedAt, link.LastUpdatedAt);
        Assert.Equal(eventKey, link.LastEventKey);
    }

    [Fact]
    public async Task AddRangeAndSetCursorAsync_WhenCursorUpdateFails_RollsBackOutboxMessages()
    {
        await fixture.ResetAsync();

        var sut = CreateSut();

        var updatedAt = new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.AddRangeAndSetCursorAsync(
                404,
                updatedAt,
                "issue:123",
                [
                    new LinkUpdate { Id = 404, Url = new Uri("https://github.com/user/missing"), Description = "Should be rolled back", TgChatIds = [1001] }
                ],
                CancellationToken.None));

        var messages = await sut.ClaimUnprocessedBatchAsync(10, 3, DefaultLock, CancellationToken.None);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task MarkProcessedAsync_HidesMessageFromUnprocessedBatch()
    {
        await fixture.ResetAsync();

        var sut = CreateSut();

        await sut.AddAsync(
            new LinkUpdate { Id = 1, Url = new Uri("https://github.com/user/repo"), Description = "Repository updated", TgChatIds = [1001] },
            CancellationToken.None);

        var before = await sut.ClaimUnprocessedBatchAsync(10, 3, DefaultLock, CancellationToken.None);
        var message = Assert.Single(before);

        await sut.MarkProcessedAsync(message.Id, CancellationToken.None);

        var after = await sut.ClaimUnprocessedBatchAsync(10, 3, DefaultLock, CancellationToken.None);

        Assert.Empty(after);
    }

    [Fact]
    public async Task MarkFailedAsync_IncrementsRetryCountAndStoresError()
    {
        await fixture.ResetAsync();

        var sut = CreateSut();

        await sut.AddAsync(
            new LinkUpdate { Id = 1, Url = new Uri("https://github.com/user/repo"), Description = "Repository updated", TgChatIds = [1001] },
            CancellationToken.None);

        var before = await sut.ClaimUnprocessedBatchAsync(10, 3, DefaultLock, CancellationToken.None);
        var message = Assert.Single(before);

        await sut.MarkFailedAsync(message.Id, "Bot is unavailable", CancellationToken.None);

        var after = await sut.ClaimUnprocessedBatchAsync(10, 3, DefaultLock, CancellationToken.None);
        var failed = Assert.Single(after);

        Assert.Equal(1, failed.RetryCount);
        Assert.Equal("Bot is unavailable", failed.Error);
    }

    [Fact]
    public async Task ClaimUnprocessedBatchAsync_WhenRetryCountReachedLimit_DoesNotReturnMessage()
    {
        await fixture.ResetAsync();

        var sut = CreateSut();

        await sut.AddAsync(
            new LinkUpdate { Id = 1, Url = new Uri("https://github.com/user/repo"), Description = "Repository updated", TgChatIds = [1001] },
            CancellationToken.None);

        var before = await sut.ClaimUnprocessedBatchAsync(10, 3, DefaultLock, CancellationToken.None);
        var message = Assert.Single(before);

        await sut.MarkFailedAsync(message.Id, "first", CancellationToken.None);
        await sut.MarkFailedAsync(message.Id, "second", CancellationToken.None);
        await sut.MarkFailedAsync(message.Id, "third", CancellationToken.None);

        var after = await sut.ClaimUnprocessedBatchAsync(
            10,
            3,
            DefaultLock,
            CancellationToken.None);

        Assert.Empty(after);
    }

    [Fact]
    public async Task ClaimUnprocessedBatchAsync_WhenSecondDispatcherClaims_DoesNotReturnAlreadyClaimedMessages()
    {
        await fixture.ResetAsync();

        var first = CreateSut();
        var second = CreateSut();

        foreach (var id in new[] { 1L, 2L, 3L })
        {
            await first.AddAsync(
                new LinkUpdate { Id = id, Url = new Uri($"https://github.com/user/repo-{id}"), Description = "Repository updated", TgChatIds = [1001] },
                CancellationToken.None);
        }

        var firstBatch = await first.ClaimUnprocessedBatchAsync(10, 3, DefaultLock, CancellationToken.None);
        var secondBatch = await second.ClaimUnprocessedBatchAsync(10, 3, DefaultLock, CancellationToken.None);

        Assert.Equal(3, firstBatch.Count);
        Assert.Empty(secondBatch);
    }

    [Fact]
    public async Task ClaimUnprocessedBatchAsync_WhenLeaseExpired_ReturnsMessageAgain()
    {
        await fixture.ResetAsync();

        var sut = CreateSut();

        await sut.AddAsync(
            new LinkUpdate { Id = 1, Url = new Uri("https://github.com/user/repo"), Description = "Repository updated", TgChatIds = [1001] },
            CancellationToken.None);

        var claimed = await sut.ClaimUnprocessedBatchAsync(10, 3, TimeSpan.Zero, CancellationToken.None);
        var reclaimed = await sut.ClaimUnprocessedBatchAsync(10, 3, DefaultLock, CancellationToken.None);

        Assert.Single(claimed);
        Assert.Single(reclaimed);
        Assert.Equal(claimed[0].Id, reclaimed[0].Id);
    }

    private PostgresOutboxStore CreateSut()
    {
        return new PostgresOutboxStore(
            fixture.DataSource,
            new SystemTextJsonOutboxMessageSerializer(),
            new ScrapperMetrics());
    }
}