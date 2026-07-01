using LinkTracker.Scrapper.Storage.Abstractions.Models;

namespace LinkTracker.Tests.Scrapper.Integration.Storage;

public abstract class LinkTrackingStoreContractTests
{
    protected abstract Task ExecuteWithSut(Func<ILinkTrackingStore, Task> test);

    [Fact]
    public async Task RegisterDeleteAndExists_WorkAsExpected()
    {
        await ExecuteWithSut(async sut =>
        {
            const long chatId = 1001;

            var registered = await sut.TryRegisterChatAsync(chatId);
            var registeredAgain = await sut.TryRegisterChatAsync(chatId);
            var existsAfterRegister = await sut.ChatExistsAsync(chatId);
            var deleted = await sut.TryDeleteChatAsync(chatId);
            var deletedAgain = await sut.TryDeleteChatAsync(chatId);
            var existsAfterDelete = await sut.ChatExistsAsync(chatId);

            Assert.True(registered);
            Assert.False(registeredAgain);
            Assert.True(existsAfterRegister);
            Assert.True(deleted);
            Assert.False(deletedAgain);
            Assert.False(existsAfterDelete);
        });
    }

    [Fact]
    public async Task AddAndReadLink_ReturnsTrackedLinkWithTags()
    {
        await ExecuteWithSut(async sut =>
        {
            const long chatId = 2001;
            var url = new Uri("https://github.com/user/repo");
            var tags = new[] { "backend", "backend", "dotnet" };

            await sut.TryRegisterChatAsync(chatId);

            var added = await sut.TryAddAsync(chatId, url, tags, []);
            var links = await sut.GetAllTrackedLinkRecordsAsync(chatId);

            Assert.NotNull(added);
            Assert.Equal(url, added!.Url);
            Assert.Equal(["backend", "dotnet"], added.Tags.OrderBy(x => x).ToArray());
            Assert.Empty(added.Filters);
            Assert.Null(added.LastUpdatedAt);
            Assert.Null(added.LastEventKey);

            var only = Assert.Single(links);
            Assert.Equal(url, only.Url);
            Assert.Equal(["backend", "dotnet"], only.Tags.OrderBy(x => x).ToArray());
            Assert.Empty(only.Filters);
            Assert.Null(only.LastUpdatedAt);
            Assert.Null(only.LastEventKey);
        });
    }

    [Fact]
    public async Task AddSameLinkTwice_ForSameChat_ReturnsNullOnSecondAttempt()
    {
        await ExecuteWithSut(async sut =>
        {
            const long chatId = 3001;
            var url = new Uri("https://stackoverflow.com/questions/79906410/example");

            await sut.TryRegisterChatAsync(chatId);

            var first = await sut.TryAddAsync(chatId, url, ["tag1"], []);
            var second = await sut.TryAddAsync(chatId, url, ["tag1"], []);

            Assert.NotNull(first);
            Assert.Null(second);
        });
    }

    [Fact]
    public async Task RemoveLink_RemovesOnlySubscriptionForCurrentChat()
    {
        await ExecuteWithSut(async sut =>
        {
            const long firstChatId = 4001;
            const long secondChatId = 4002;
            var url = new Uri("https://github.com/user/shared-repo");

            await sut.TryRegisterChatAsync(firstChatId);
            await sut.TryRegisterChatAsync(secondChatId);

            await sut.TryAddAsync(firstChatId, url, ["alpha"], []);
            await sut.TryAddAsync(secondChatId, url, ["beta"], []);

            var removed = await sut.TryRemoveAsync(firstChatId, url);
            var firstChatLinks = await sut.GetAllTrackedLinkRecordsAsync(firstChatId);
            var secondChatLinks = await sut.GetAllTrackedLinkRecordsAsync(secondChatId);
            var subscriptions = await sut.GetAllSubscriptionsAsync();

            Assert.NotNull(removed);
            Assert.Equal(url, removed!.Url);

            Assert.Empty(firstChatLinks);

            var secondOnly = Assert.Single(secondChatLinks);
            Assert.Equal(url, secondOnly.Url);

            var subscription = Assert.Single(subscriptions);
            Assert.Equal(url, subscription.Url);
            Assert.Single(subscription.TgChatIds);
            Assert.Contains(secondChatId, subscription.TgChatIds);
        });
    }

    [Fact]
    public async Task RemoveMissingLink_ReturnsNull()
    {
        await ExecuteWithSut(async sut =>
        {
            const long chatId = 5001;
            var url = new Uri("https://github.com/user/missing");

            await sut.TryRegisterChatAsync(chatId);

            var removed = await sut.TryRemoveAsync(chatId, url);

            Assert.Null(removed);
        });
    }

    [Fact]
    public async Task SetCursor_UpdatesTrackedLinkRecordAndSubscription()
    {
        await ExecuteWithSut(async sut =>
        {
            const long chatId = 6001;
            var url = new Uri("https://github.com/user/repo-updated");
            var updatedAt = new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);
            const string eventKey = "issue:123";

            await sut.TryRegisterChatAsync(chatId);
            var added = await sut.TryAddAsync(chatId, url, ["sync"], []);
            Assert.NotNull(added);

            await sut.SetCursorAsync(added!.Id, updatedAt, eventKey);

            var links = await sut.GetAllTrackedLinkRecordsAsync(chatId);
            var only = Assert.Single(links);

            Assert.Equal(updatedAt, only.LastUpdatedAt);
            Assert.Equal(eventKey, only.LastEventKey);

            var subscriptions = await sut.GetAllSubscriptionsAsync();
            var subscription = Assert.Single(subscriptions);

            Assert.Equal(updatedAt, subscription.LastUpdatedAt);
            Assert.Equal(eventKey, subscription.LastEventKey);
        });
    }

    [Fact]
    public async Task SetCursor_ByLinkId_UpdatesSharedLinkForAllSubscribers()
    {
        await ExecuteWithSut(async sut =>
        {
            const long firstChatId = 6101;
            const long secondChatId = 6102;
            var url = new Uri("https://github.com/user/shared-repo");
            var updatedAt = new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);
            const string eventKey = "pr:200";

            await sut.TryRegisterChatAsync(firstChatId);
            await sut.TryRegisterChatAsync(secondChatId);

            var firstAdded = await sut.TryAddAsync(firstChatId, url, ["alpha"], []);
            var secondAdded = await sut.TryAddAsync(secondChatId, url, ["beta"], []);

            Assert.NotNull(firstAdded);
            Assert.NotNull(secondAdded);
            Assert.Equal(firstAdded!.Id, secondAdded!.Id);

            await sut.SetCursorAsync(firstAdded.Id, updatedAt, eventKey);

            var firstLinks = await sut.GetAllTrackedLinkRecordsAsync(firstChatId);
            var secondLinks = await sut.GetAllTrackedLinkRecordsAsync(secondChatId);

            var firstOnly = Assert.Single(firstLinks);
            var secondOnly = Assert.Single(secondLinks);

            Assert.Equal(updatedAt, firstOnly.LastUpdatedAt);
            Assert.Equal(eventKey, firstOnly.LastEventKey);

            Assert.Equal(updatedAt, secondOnly.LastUpdatedAt);
            Assert.Equal(eventKey, secondOnly.LastEventKey);

            var subscriptions = await sut.GetAllSubscriptionsAsync();
            var subscription = Assert.Single(subscriptions);

            Assert.Equal(firstAdded.Id, subscription.Id);
            Assert.Equal(updatedAt, subscription.LastUpdatedAt);
            Assert.Equal(eventKey, subscription.LastEventKey);
            Assert.Equal(2, subscription.TgChatIds.Count);
        });
    }

    [Fact]
    public async Task SetCursor_AllowsNullEventKey()
    {
        await ExecuteWithSut(async sut =>
        {
            const long chatId = 6201;
            var url = new Uri("https://github.com/user/repo-baseline");
            var updatedAt = new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);

            await sut.TryRegisterChatAsync(chatId);
            var added = await sut.TryAddAsync(chatId, url, ["baseline"], []);
            Assert.NotNull(added);

            await sut.SetCursorAsync(added!.Id, updatedAt, null);

            var links = await sut.GetAllTrackedLinkRecordsAsync(chatId);
            var only = Assert.Single(links);

            Assert.Equal(updatedAt, only.LastUpdatedAt);
            Assert.Null(only.LastEventKey);

            var subscriptions = await sut.GetAllSubscriptionsAsync();
            var subscription = Assert.Single(subscriptions);

            Assert.Equal(updatedAt, subscription.LastUpdatedAt);
            Assert.Null(subscription.LastEventKey);
        });
    }

    [Fact]
    public async Task TagCrud_WorksForExistingSubscription()
    {
        await ExecuteWithSut(async sut =>
        {
            const long chatId = 7001;
            var url = new Uri("https://github.com/user/repo-tags");

            await sut.TryRegisterChatAsync(chatId);
            await sut.TryAddAsync(chatId, url, ["alpha"], []);

            var afterAddTag = await sut.TryAddTagAsync(chatId, url, "beta");
            var tagsAfterAdd = await sut.GetTagsAsync(chatId);

            var renamed = await sut.TryRenameTagAsync(chatId, "beta", "gamma");
            var tagsAfterRename = await sut.GetTagsAsync(chatId);

            var deleted = await sut.TryDeleteTagAsync(chatId, "alpha");
            var finalLinks = await sut.GetAllTrackedLinkRecordsAsync(chatId);
            var finalTags = await sut.GetTagsAsync(chatId);

            Assert.NotNull(afterAddTag);
            Assert.Equal(["alpha", "beta"], afterAddTag!.Tags.OrderBy(x => x).ToArray());
            Assert.Equal(["alpha", "beta"], tagsAfterAdd.OrderBy(x => x).ToArray());

            Assert.True(renamed);
            Assert.Equal(["alpha", "gamma"], tagsAfterRename.OrderBy(x => x).ToArray());

            Assert.True(deleted);

            var only = Assert.Single(finalLinks);
            Assert.Equal(["gamma"], only.Tags.OrderBy(x => x).ToArray());
            Assert.Equal(["gamma"], finalTags.OrderBy(x => x).ToArray());
        });
    }

    [Theory]
    [InlineData("add-tag-missing-subscription")]
    [InlineData("rename-missing-tag")]
    [InlineData("delete-missing-tag")]
    public async Task TagCrud_NegativeCases_ReturnExpectedResult(string scenario)
    {
        await ExecuteWithSut(async sut =>
        {
            const long chatId = 7002;
            var url = new Uri("https://github.com/user/repo");

            await sut.TryRegisterChatAsync(chatId);

            if (scenario is not "add-tag-missing-subscription")
            {
                await sut.TryAddAsync(chatId, url, ["alpha"], []);
            }

            switch (scenario)
            {
                case "add-tag-missing-subscription":
                    {
                        var result = await sut.TryAddTagAsync(chatId, url, "beta");
                        Assert.Null(result);
                        break;
                    }

                case "rename-missing-tag":
                    {
                        var result = await sut.TryRenameTagAsync(chatId, "missing", "gamma");
                        Assert.False(result);
                        break;
                    }

                case "delete-missing-tag":
                    {
                        var result = await sut.TryDeleteTagAsync(chatId, "missing");
                        Assert.False(result);
                        break;
                    }

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
            }
        });
    }

    [Fact]
    public async Task RenameTag_DoesNotCreateDuplicates_WhenTargetAlreadyExists()
    {
        await ExecuteWithSut(async sut =>
        {
            const long chatId = 7003;
            var url = new Uri("https://github.com/user/repo");

            await sut.TryRegisterChatAsync(chatId);
            await sut.TryAddAsync(chatId, url, ["alpha", "beta"], []);

            var renamed = await sut.TryRenameTagAsync(chatId, "alpha", "beta");
            var links = await sut.GetAllTrackedLinkRecordsAsync(chatId);
            var tags = await sut.GetTagsAsync(chatId);

            Assert.True(renamed);

            var only = Assert.Single(links);
            Assert.Equal(["beta"], only.Tags.OrderBy(x => x).ToArray());
            Assert.Equal(["beta"], tags.OrderBy(x => x).ToArray());
        });
    }

    [Fact]
    public async Task GetSubscriptionsBatchAsync_ReturnsSubscriptionsOrderedById()
    {
        await ExecuteWithSut(async sut =>
        {
            const long firstChatId = 8001;
            const long secondChatId = 8002;
            const long thirdChatId = 8003;

            await sut.TryRegisterChatAsync(firstChatId);
            await sut.TryRegisterChatAsync(secondChatId);
            await sut.TryRegisterChatAsync(thirdChatId);

            var first = await sut.TryAddAsync(
                firstChatId,
                new Uri("https://github.com/user/repo-1"),
                [],
                []);

            var second = await sut.TryAddAsync(
                secondChatId,
                new Uri("https://github.com/user/repo-2"),
                [],
                []);

            var third = await sut.TryAddAsync(
                thirdChatId,
                new Uri("https://github.com/user/repo-3"),
                [],
                []);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotNull(third);

            var batch = await sut.GetSubscriptionsBatchAsync(null, 10);

            Assert.Equal(3, batch.Count);

            var actualIds = batch.Select(x => x.Id).ToArray();
            var orderedIds = actualIds.OrderBy(x => x).ToArray();

            Assert.Equal(orderedIds, actualIds);
        });
    }

    [Fact]
    public async Task GetSubscriptionsBatchAsync_ReturnsOnlyItemsAfterLinkId()
    {
        await ExecuteWithSut(async sut =>
        {
            const long firstChatId = 8101;
            const long secondChatId = 8102;
            const long thirdChatId = 8103;

            await sut.TryRegisterChatAsync(firstChatId);
            await sut.TryRegisterChatAsync(secondChatId);
            await sut.TryRegisterChatAsync(thirdChatId);

            var first = await sut.TryAddAsync(
                firstChatId,
                new Uri("https://github.com/user/repo-1"),
                [],
                []);

            var second = await sut.TryAddAsync(
                secondChatId,
                new Uri("https://github.com/user/repo-2"),
                [],
                []);

            var third = await sut.TryAddAsync(
                thirdChatId,
                new Uri("https://github.com/user/repo-3"),
                [],
                []);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotNull(third);

            var batch = await sut.GetSubscriptionsBatchAsync(first!.Id, 10);

            Assert.Equal(
                [second!.Id, third!.Id],
                batch.Select(x => x.Id).ToArray());
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task GetSubscriptionsBatchAsync_RespectsBatchSize(int batchSize)
    {
        await ExecuteWithSut(async sut =>
        {
            const long firstChatId = 8201;
            const long secondChatId = 8202;
            const long thirdChatId = 8203;
            const long fourthChatId = 8204;

            await sut.TryRegisterChatAsync(firstChatId);
            await sut.TryRegisterChatAsync(secondChatId);
            await sut.TryRegisterChatAsync(thirdChatId);
            await sut.TryRegisterChatAsync(fourthChatId);

            await sut.TryAddAsync(firstChatId, new Uri("https://github.com/user/repo-1"), [], []);
            await sut.TryAddAsync(secondChatId, new Uri("https://github.com/user/repo-2"), [], []);
            await sut.TryAddAsync(thirdChatId, new Uri("https://github.com/user/repo-3"), [], []);
            await sut.TryAddAsync(fourthChatId, new Uri("https://github.com/user/repo-4"), [], []);

            var batch = await sut.GetSubscriptionsBatchAsync(null, batchSize);

            Assert.Equal(batchSize, batch.Count);
        });
    }

    [Fact]
    public async Task GetSubscriptionsBatchAsync_ReturnsSharedLinkOnceWithAllSubscribers()
    {
        await ExecuteWithSut(async sut =>
        {
            const long firstChatId = 8301;
            const long secondChatId = 8302;
            var url = new Uri("https://github.com/user/shared-repo");

            await sut.TryRegisterChatAsync(firstChatId);
            await sut.TryRegisterChatAsync(secondChatId);

            var first = await sut.TryAddAsync(firstChatId, url, ["alpha"], []);
            var second = await sut.TryAddAsync(secondChatId, url, ["beta"], []);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(first!.Id, second!.Id);

            var batch = await sut.GetSubscriptionsBatchAsync(null, 10);

            var subscription = Assert.Single(batch);

            Assert.Equal(first.Id, subscription.Id);
            Assert.Equal(url, subscription.Url);
            Assert.Equal(2, subscription.TgChatIds.Count);
            Assert.Contains(firstChatId, subscription.TgChatIds);
            Assert.Contains(secondChatId, subscription.TgChatIds);
        });
    }

    [Fact]
    public async Task GetSubscriptionsBatchAsync_ReturnsCursorFields()
    {
        await ExecuteWithSut(async sut =>
        {
            const long chatId = 8401;
            var url = new Uri("https://github.com/user/repo");
            var updatedAt = new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);
            const string eventKey = "issue:123";

            await sut.TryRegisterChatAsync(chatId);

            var added = await sut.TryAddAsync(chatId, url, [], []);
            Assert.NotNull(added);

            await sut.SetCursorAsync(added!.Id, updatedAt, eventKey);

            var batch = await sut.GetSubscriptionsBatchAsync(null, 10);

            var subscription = Assert.Single(batch);

            Assert.Equal(added.Id, subscription.Id);
            Assert.Equal(updatedAt, subscription.LastUpdatedAt);
            Assert.Equal(eventKey, subscription.LastEventKey);
        });
    }
}