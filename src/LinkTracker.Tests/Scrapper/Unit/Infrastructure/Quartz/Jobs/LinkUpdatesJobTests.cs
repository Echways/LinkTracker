using LinkTracker.Scrapper.Application.Abstractions.Updates;
using LinkTracker.Scrapper.Application.Clients.Bot;
using LinkTracker.Scrapper.Application.Models.Updates;
using LinkTracker.Scrapper.Infrastructure.Outbox.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Outbox.Configuration;
using LinkTracker.Scrapper.Infrastructure.Quartz.Configuration;
using LinkTracker.Scrapper.Infrastructure.Quartz.Jobs;
using LinkTracker.Scrapper.Infrastructure.Telemetry;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using LinkTracker.Shared.Constants;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Quartz;

namespace LinkTracker.Tests.Scrapper.Unit.Infrastructure.Quartz.Jobs;

[Trait("Module", "Scrapper")]
[Trait("Category", "Unit")]
public sealed class LinkUpdatesJobTests
{
    private const int DefaultBatchSize = 100;
    private const int DefaultMaxDegreeOfParallelism = 1;
    private const bool DefaultOutboxEnabled = false;

    [Fact]
    public async Task Execute_WhenTrackedLinkHasEvents_SendsUpdateOnlyToSubscribers_AndUpdatesCursor()
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var githubHandler = Substitute.For<ILinkUpdateHandler>();
        var stackOverflowHandler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        var previousUpdatedAt = new DateTimeOffset(2025, 3, 10, 10, 0, 0, TimeSpan.Zero);
        var newUpdatedAt = new DateTimeOffset(2025, 3, 10, 12, 0, 0, TimeSpan.Zero);
        const string newEventKey = "issue:123";

        var subscription = new TrackedLinkSubscription
        {
            Id = 10,
            Url = new Uri("https://github.com/user/repo"),
            TgChatIds = [1001L, 1002L],
            LastUpdatedAt = previousUpdatedAt,
            LastEventKey = "issue:122"
        };

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([subscription]);

        trackingStore.GetSubscriptionsBatchAsync(subscription.Id, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        githubHandler.CanHandle(subscription.Url).Returns(true);
        stackOverflowHandler.CanHandle(subscription.Url).Returns(false);

        githubHandler.CheckAsync(subscription, Arg.Any<CancellationToken>())
            .Returns(new LinkCheckResult
            {
                NewLastUpdatedAt = newUpdatedAt,
                NewLastEventKey = newEventKey,
                Events =
                [
                    new LinkEvent
                    {
                        SourceKind = LinkSourceKind.GitHub,
                        EventKind = LinkEventKind.Issue,
                        Title = "Issue title",
                        UserName = "octocat",
                        CreatedAt = newUpdatedAt,
                        EventKey = newEventKey,
                        Body = "Issue body",
                        ResourceUrl = new Uri("https://github.com/user/repo/issues/123")
                    }
                ]
            });

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [githubHandler, stackOverflowHandler],
            botClient,
            outboxStore,
            logger);

        await sut.Execute(quartzContext);

        await botClient.Received(1).SendUpdateAsync(
            Arg.Is<LinkUpdate>(x =>
                x.Id == subscription.Id &&
                x.Url == subscription.Url &&
                x.TgChatIds.Count == 2 &&
                x.TgChatIds.Contains(1001L) &&
                x.TgChatIds.Contains(1002L) &&
                !x.TgChatIds.Contains(9999L) &&
                x.Description.Contains("Заголовок: Issue")),
            Arg.Any<CancellationToken>());

        await trackingStore.Received(1).SetCursorAsync(
            subscription.Id,
            newUpdatedAt,
            newEventKey,
            Arg.Any<CancellationToken>());

        await githubHandler.Received(1)
            .CheckAsync(subscription, Arg.Any<CancellationToken>());

        await stackOverflowHandler.DidNotReceive()
            .CheckAsync(Arg.Any<TrackedLinkSubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenHandlerThrows_SendsFailedReport_AndDoesNotUpdateCursor()
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var githubHandler = Substitute.For<ILinkUpdateHandler>();
        var stackOverflowHandler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        var subscription = new TrackedLinkSubscription { Id = 10, Url = new Uri("https://github.com/user/repo"), TgChatIds = [1001L] };

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([subscription]);

        trackingStore.GetSubscriptionsBatchAsync(subscription.Id, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        githubHandler.CanHandle(subscription.Url).Returns(true);
        stackOverflowHandler.CanHandle(subscription.Url).Returns(false);

        githubHandler.CheckAsync(subscription, Arg.Any<CancellationToken>())
            .Returns<Task<LinkCheckResult>>(_ => throw new HttpRequestException("boom"));

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [githubHandler, stackOverflowHandler],
            botClient,
            outboxStore,
            logger);

        await sut.Execute(quartzContext);

        await trackingStore.DidNotReceive()
            .SetCursorAsync(
                Arg.Any<long>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());

        await botClient.Received(1)
            .SendUpdateAsync(
                Arg.Is<LinkUpdate>(x =>
                    x.TgChatIds.Count == 1 &&
                    x.TgChatIds.Contains(1001L) &&
                    x.Description.Contains(SystemMessageMarkers.FailedLinkReport) &&
                    x.Description.Contains(subscription.Url.ToString())),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenMultipleEvents_SendsEachEventAsSeparateUpdate_AndUpdatesCursorToLatestEvent()
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var githubHandler = Substitute.For<ILinkUpdateHandler>();
        var stackOverflowHandler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        var previousUpdatedAt = new DateTimeOffset(2025, 3, 10, 10, 0, 0, TimeSpan.Zero);
        var firstEventCreatedAt = new DateTimeOffset(2025, 3, 10, 11, 0, 0, TimeSpan.Zero);
        var secondEventCreatedAt = new DateTimeOffset(2025, 3, 10, 12, 0, 0, TimeSpan.Zero);

        var subscription = new TrackedLinkSubscription
        {
            Id = 10,
            Url = new Uri("https://github.com/user/repo"),
            TgChatIds = [1001L],
            LastUpdatedAt = previousUpdatedAt,
            LastEventKey = "issue:120"
        };

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([subscription]);

        trackingStore.GetSubscriptionsBatchAsync(subscription.Id, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        githubHandler.CanHandle(subscription.Url).Returns(true);
        stackOverflowHandler.CanHandle(subscription.Url).Returns(false);

        githubHandler.CheckAsync(subscription, Arg.Any<CancellationToken>())
            .Returns(new LinkCheckResult
            {
                NewLastUpdatedAt = secondEventCreatedAt,
                NewLastEventKey = "pr:200",
                Events =
                [
                    new LinkEvent
                    {
                        SourceKind = LinkSourceKind.GitHub,
                        EventKind = LinkEventKind.Issue,
                        Title = "Issue title",
                        UserName = "octocat",
                        CreatedAt = firstEventCreatedAt,
                        EventKey = "issue:123",
                        Body = "Issue body",
                        ResourceUrl = new Uri("https://github.com/user/repo/issues/123")
                    },
                    new LinkEvent
                    {
                        SourceKind = LinkSourceKind.GitHub,
                        EventKind = LinkEventKind.PullRequest,
                        Title = "PR title",
                        UserName = "octocat",
                        CreatedAt = secondEventCreatedAt,
                        EventKey = "pr:200",
                        Body = "PR body",
                        ResourceUrl = new Uri("https://github.com/user/repo/pull/200")
                    }
                ]
            });

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [githubHandler, stackOverflowHandler],
            botClient,
            outboxStore,
            logger);

        await sut.Execute(quartzContext);

        await botClient.Received(2)
            .SendUpdateAsync(Arg.Any<LinkUpdate>(), Arg.Any<CancellationToken>());

        await trackingStore.Received(1)
            .SetCursorAsync(
                subscription.Id,
                secondEventCreatedAt,
                "pr:200",
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Execute_WhenNoEvents_DoesNotSendUpdate_AndUpdatesCursorOnlyWhenItChanged(
        bool cursorChanged,
        bool shouldUpdateCursor)
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var githubHandler = Substitute.For<ILinkUpdateHandler>();
        var stackOverflowHandler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        var previousUpdatedAt = new DateTimeOffset(2025, 3, 10, 10, 0, 0, TimeSpan.Zero);
        var newUpdatedAt = new DateTimeOffset(2025, 3, 10, 11, 0, 0, TimeSpan.Zero);

        var subscription = new TrackedLinkSubscription
        {
            Id = 10,
            Url = new Uri("https://github.com/user/repo"),
            TgChatIds = [1001L, 1002L],
            LastUpdatedAt = previousUpdatedAt,
            LastEventKey = "issue:122"
        };

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([subscription]);

        trackingStore.GetSubscriptionsBatchAsync(subscription.Id, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        githubHandler.CanHandle(subscription.Url).Returns(true);
        stackOverflowHandler.CanHandle(subscription.Url).Returns(false);

        githubHandler.CheckAsync(subscription, Arg.Any<CancellationToken>())
            .Returns(new LinkCheckResult { NewLastUpdatedAt = cursorChanged ? newUpdatedAt : subscription.LastUpdatedAt, NewLastEventKey = cursorChanged ? "issue:123" : subscription.LastEventKey, Events = [] });

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [githubHandler, stackOverflowHandler],
            botClient,
            outboxStore,
            logger);

        await sut.Execute(quartzContext);

        await botClient.DidNotReceive()
            .SendUpdateAsync(Arg.Any<LinkUpdate>(), Arg.Any<CancellationToken>());

        if (shouldUpdateCursor)
        {
            await trackingStore.Received(1)
                .SetCursorAsync(
                    subscription.Id,
                    newUpdatedAt,
                    "issue:123",
                    Arg.Any<CancellationToken>());
        }
        else
        {
            await trackingStore.DidNotReceive()
                .SetCursorAsync(
                    Arg.Any<long>(),
                    Arg.Any<DateTimeOffset>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task Execute_WhenNoHandlerCanHandleUrl_SkipsSubscription()
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var githubHandler = Substitute.For<ILinkUpdateHandler>();
        var stackOverflowHandler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        var subscription = new TrackedLinkSubscription { Id = 10, Url = new Uri("https://example.com/page"), TgChatIds = [1001L] };

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([subscription]);

        trackingStore.GetSubscriptionsBatchAsync(subscription.Id, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        githubHandler.CanHandle(subscription.Url).Returns(false);
        stackOverflowHandler.CanHandle(subscription.Url).Returns(false);

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [githubHandler, stackOverflowHandler],
            botClient,
            outboxStore,
            logger);

        await sut.Execute(quartzContext);

        await githubHandler.DidNotReceive()
            .CheckAsync(Arg.Any<TrackedLinkSubscription>(), Arg.Any<CancellationToken>());

        await stackOverflowHandler.DidNotReceive()
            .CheckAsync(Arg.Any<TrackedLinkSubscription>(), Arg.Any<CancellationToken>());

        await trackingStore.DidNotReceive()
            .SetCursorAsync(
                Arg.Any<long>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());

        await botClient.DidNotReceive()
            .SendUpdateAsync(Arg.Any<LinkUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenSubscriptionsExceedBatchSize_ProcessesMultipleBatchesSequentially()
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var handler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        var first = new TrackedLinkSubscription { Id = 10, Url = new Uri("https://github.com/user/repo1"), TgChatIds = [1001L] };

        var second = new TrackedLinkSubscription { Id = 20, Url = new Uri("https://github.com/user/repo2"), TgChatIds = [1002L] };

        trackingStore.GetSubscriptionsBatchAsync(null, 1, Arg.Any<CancellationToken>())
            .Returns([first]);

        trackingStore.GetSubscriptionsBatchAsync(first.Id, 1, Arg.Any<CancellationToken>())
            .Returns([second]);

        trackingStore.GetSubscriptionsBatchAsync(second.Id, 1, Arg.Any<CancellationToken>())
            .Returns([]);

        handler.CanHandle(first.Url).Returns(true);
        handler.CanHandle(second.Url).Returns(true);

        handler.CheckAsync(first, Arg.Any<CancellationToken>())
            .Returns(new LinkCheckResult());

        handler.CheckAsync(second, Arg.Any<CancellationToken>())
            .Returns(new LinkCheckResult());

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [handler],
            botClient,
            outboxStore,
            logger,
            1);

        await sut.Execute(quartzContext);

        await trackingStore.Received(1)
            .GetSubscriptionsBatchAsync(null, 1, Arg.Any<CancellationToken>());

        await trackingStore.Received(1)
            .GetSubscriptionsBatchAsync(first.Id, 1, Arg.Any<CancellationToken>());

        await trackingStore.Received(1)
            .GetSubscriptionsBatchAsync(second.Id, 1, Arg.Any<CancellationToken>());

        await handler.Received(1)
            .CheckAsync(first, Arg.Any<CancellationToken>());

        await handler.Received(1)
            .CheckAsync(second, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenOneSubscriptionFails_ContinuesWithRemainingSubscriptionsInBatch()
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var handler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        var failedSubscription = new TrackedLinkSubscription { Id = 10, Url = new Uri("https://github.com/user/repo1"), TgChatIds = [1001L] };

        var successfulSubscription = new TrackedLinkSubscription { Id = 20, Url = new Uri("https://github.com/user/repo2"), TgChatIds = [1002L] };

        var eventCreatedAt = new DateTimeOffset(2025, 3, 10, 12, 0, 0, TimeSpan.Zero);

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([failedSubscription, successfulSubscription]);

        trackingStore.GetSubscriptionsBatchAsync(successfulSubscription.Id, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        handler.CanHandle(failedSubscription.Url).Returns(true);
        handler.CanHandle(successfulSubscription.Url).Returns(true);

        handler.CheckAsync(failedSubscription, Arg.Any<CancellationToken>())
            .Returns<Task<LinkCheckResult>>(_ => throw new HttpRequestException("boom"));

        handler.CheckAsync(successfulSubscription, Arg.Any<CancellationToken>())
            .Returns(new LinkCheckResult
            {
                NewLastUpdatedAt = eventCreatedAt,
                NewLastEventKey = "issue:500",
                Events =
                [
                    new LinkEvent
                    {
                        SourceKind = LinkSourceKind.GitHub,
                        EventKind = LinkEventKind.Issue,
                        Title = "Issue title",
                        UserName = "octocat",
                        CreatedAt = eventCreatedAt,
                        EventKey = "issue:500",
                        Body = "Issue body",
                        ResourceUrl = new Uri("https://github.com/user/repo2/issues/500")
                    }
                ]
            });

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [handler],
            botClient,
            outboxStore,
            logger);

        await sut.Execute(quartzContext);

        await handler.Received(1).CheckAsync(failedSubscription, Arg.Any<CancellationToken>());
        await handler.Received(1).CheckAsync(successfulSubscription, Arg.Any<CancellationToken>());

        await botClient.Received(2)
            .SendUpdateAsync(Arg.Any<LinkUpdate>(), Arg.Any<CancellationToken>());

        await botClient.Received(1)
            .SendUpdateAsync(
                Arg.Is<LinkUpdate>(x =>
                    x.Id == successfulSubscription.Id &&
                    x.Url == successfulSubscription.Url &&
                    x.TgChatIds.Count == 1 &&
                    x.TgChatIds.Contains(1002L) &&
                    !x.Description.Contains(SystemMessageMarkers.FailedLinkReport) &&
                    x.Description.Contains("Заголовок: Issue")),
                Arg.Any<CancellationToken>());

        await botClient.Received(1)
            .SendUpdateAsync(
                Arg.Is<LinkUpdate>(x =>
                    x.TgChatIds.Count == 1 &&
                    x.TgChatIds.Contains(1001L) &&
                    x.Description.Contains(SystemMessageMarkers.FailedLinkReport) &&
                    x.Description.Contains(failedSubscription.Url.ToString())),
                Arg.Any<CancellationToken>());

        await trackingStore.Received(1)
            .SetCursorAsync(
                successfulSubscription.Id,
                eventCreatedAt,
                "issue:500",
                Arg.Any<CancellationToken>());

        await trackingStore.DidNotReceive()
            .SetCursorAsync(
                failedSubscription.Id,
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenStoreReturnsEmptyBatch_StopsProcessing()
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var handler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [handler],
            botClient,
            outboxStore,
            logger);

        await sut.Execute(quartzContext);

        await handler.DidNotReceive()
            .CheckAsync(Arg.Any<TrackedLinkSubscription>(), Arg.Any<CancellationToken>());

        await botClient.DidNotReceive()
            .SendUpdateAsync(Arg.Any<LinkUpdate>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(77)]
    [InlineData(500)]
    public async Task Execute_UsesConfiguredBatchSize(int configuredBatchSize)
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var handler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        trackingStore.GetSubscriptionsBatchAsync(null, configuredBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [handler],
            botClient,
            outboxStore,
            logger,
            configuredBatchSize);

        await sut.Execute(quartzContext);

        await trackingStore.Received(1)
            .GetSubscriptionsBatchAsync(null, configuredBatchSize, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    public async Task Execute_AcceptsConfiguredParallelismAndProcessesBatch(int configuredMaxDegreeOfParallelism)
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var handler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        var firstSubscription = new TrackedLinkSubscription { Id = 10, Url = new Uri("https://github.com/user/repo1"), TgChatIds = [1001L] };

        var secondSubscription = new TrackedLinkSubscription { Id = 20, Url = new Uri("https://github.com/user/repo2"), TgChatIds = [1002L] };

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([firstSubscription, secondSubscription]);

        trackingStore.GetSubscriptionsBatchAsync(secondSubscription.Id, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        handler.CanHandle(firstSubscription.Url).Returns(true);
        handler.CanHandle(secondSubscription.Url).Returns(true);

        handler.CheckAsync(firstSubscription, Arg.Any<CancellationToken>())
            .Returns(new LinkCheckResult());

        handler.CheckAsync(secondSubscription, Arg.Any<CancellationToken>())
            .Returns(new LinkCheckResult());

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [handler],
            botClient,
            outboxStore,
            logger,
            maxDegreeOfParallelism: configuredMaxDegreeOfParallelism);

        await sut.Execute(quartzContext);

        await trackingStore.Received(1)
            .GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>());

        await handler.Received(1)
            .CheckAsync(firstSubscription, Arg.Any<CancellationToken>());

        await handler.Received(1)
            .CheckAsync(secondSubscription, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenParallelismIsGreaterThanOne_ProcessesMultipleSubscriptionsConcurrently()
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var handler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        var firstSubscription = new TrackedLinkSubscription { Id = 10, Url = new Uri("https://github.com/user/repo1"), TgChatIds = [1001L] };

        var secondSubscription = new TrackedLinkSubscription { Id = 20, Url = new Uri("https://github.com/user/repo2"), TgChatIds = [1002L] };

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([firstSubscription, secondSubscription]);

        trackingStore.GetSubscriptionsBatchAsync(secondSubscription.Id, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        handler.CanHandle(firstSubscription.Url).Returns(true);
        handler.CanHandle(secondSubscription.Url).Returns(true);

        var currentConcurrency = 0;
        var maxObservedConcurrency = 0;

        handler.CheckAsync(Arg.Any<TrackedLinkSubscription>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var concurrency = Interlocked.Increment(ref currentConcurrency);
                UpdateMaxObservedConcurrency(ref maxObservedConcurrency, concurrency);

                try
                {
                    await Task.Delay(100);
                    return new LinkCheckResult();
                }
                finally
                {
                    Interlocked.Decrement(ref currentConcurrency);
                }
            });

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [handler],
            botClient,
            outboxStore,
            logger,
            maxDegreeOfParallelism: 2);

        await sut.Execute(quartzContext);

        Assert.True(maxObservedConcurrency >= 2);
    }

    [Fact]
    public async Task Execute_WhenSeveralSubscriptionsFail_GroupsFailedReportsByChatId()
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var handler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        var firstFailedSubscription = new TrackedLinkSubscription { Id = 10, Url = new Uri("https://github.com/user/repo1"), TgChatIds = [1001L, 1002L] };

        var secondFailedSubscription = new TrackedLinkSubscription { Id = 20, Url = new Uri("https://github.com/user/repo2"), TgChatIds = [1002L] };

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([firstFailedSubscription, secondFailedSubscription]);

        trackingStore.GetSubscriptionsBatchAsync(secondFailedSubscription.Id, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        handler.CanHandle(firstFailedSubscription.Url).Returns(true);
        handler.CanHandle(secondFailedSubscription.Url).Returns(true);

        handler.CheckAsync(firstFailedSubscription, Arg.Any<CancellationToken>())
            .Returns<Task<LinkCheckResult>>(_ => throw new HttpRequestException("boom-1"));

        handler.CheckAsync(secondFailedSubscription, Arg.Any<CancellationToken>())
            .Returns<Task<LinkCheckResult>>(_ => throw new HttpRequestException("boom-2"));

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [handler],
            botClient,
            outboxStore,
            logger);

        await sut.Execute(quartzContext);

        await botClient.Received(2)
            .SendUpdateAsync(Arg.Any<LinkUpdate>(), Arg.Any<CancellationToken>());

        await botClient.Received(1)
            .SendUpdateAsync(
                Arg.Is<LinkUpdate>(x =>
                    x.TgChatIds.Count == 1 &&
                    x.TgChatIds.Contains(1001L) &&
                    x.Description.Contains(SystemMessageMarkers.FailedLinkReport) &&
                    x.Description.Contains(firstFailedSubscription.Url.ToString()) &&
                    !x.Description.Contains(secondFailedSubscription.Url.ToString())),
                Arg.Any<CancellationToken>());

        await botClient.Received(1)
            .SendUpdateAsync(
                Arg.Is<LinkUpdate>(x =>
                    x.TgChatIds.Count == 1 &&
                    x.TgChatIds.Contains(1002L) &&
                    x.Description.Contains(SystemMessageMarkers.FailedLinkReport) &&
                    x.Description.Contains(firstFailedSubscription.Url.ToString()) &&
                    x.Description.Contains(secondFailedSubscription.Url.ToString())),
                Arg.Any<CancellationToken>());

        await trackingStore.DidNotReceive()
            .SetCursorAsync(
                Arg.Any<long>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenEventBodyLongerThan200_TrimsPreviewInSentUpdate()
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var handler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        string longBody = new('a', 250);
        var eventCreatedAt = new DateTimeOffset(2025, 3, 10, 12, 0, 0, TimeSpan.Zero);

        var subscription = new TrackedLinkSubscription { Id = 10, Url = new Uri("https://github.com/user/repo"), TgChatIds = [1001L] };

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([subscription]);

        trackingStore.GetSubscriptionsBatchAsync(subscription.Id, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        handler.CanHandle(subscription.Url).Returns(true);

        handler.CheckAsync(subscription, Arg.Any<CancellationToken>())
            .Returns(new LinkCheckResult
            {
                NewLastUpdatedAt = eventCreatedAt,
                NewLastEventKey = "issue:123",
                Events =
                [
                    new LinkEvent
                    {
                        SourceKind = LinkSourceKind.GitHub,
                        EventKind = LinkEventKind.Issue,
                        Title = "Issue title",
                        UserName = "octocat",
                        CreatedAt = eventCreatedAt,
                        EventKey = "issue:123",
                        Body = longBody,
                        ResourceUrl = new Uri("https://github.com/user/repo/issues/123")
                    }
                ]
            });

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [handler],
            botClient,
            outboxStore,
            logger);

        await sut.Execute(quartzContext);

        await botClient.Received(1)
            .SendUpdateAsync(
                Arg.Is<LinkUpdate>(x =>
                    x.Description.Contains("Фрагмент:") &&
                    x.Description.Contains(new string('a', 200) + "...") &&
                    !x.Description.Contains(new string('a', 201))),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenOutboxEnabledAndTrackedLinkHasEvents_SavesUpdatesToOutboxAndDoesNotSendDirectly()
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var handler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        var previousUpdatedAt = new DateTimeOffset(2025, 3, 10, 10, 0, 0, TimeSpan.Zero);
        var newUpdatedAt = new DateTimeOffset(2025, 3, 10, 12, 0, 0, TimeSpan.Zero);
        const string newEventKey = "issue:123";

        var subscription = new TrackedLinkSubscription
        {
            Id = 10,
            Url = new Uri("https://github.com/user/repo"),
            TgChatIds = [1001L, 1002L],
            LastUpdatedAt = previousUpdatedAt,
            LastEventKey = "issue:122"
        };

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([subscription]);

        trackingStore.GetSubscriptionsBatchAsync(subscription.Id, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        handler.CanHandle(subscription.Url).Returns(true);

        handler.CheckAsync(subscription, Arg.Any<CancellationToken>())
            .Returns(new LinkCheckResult
            {
                NewLastUpdatedAt = newUpdatedAt,
                NewLastEventKey = newEventKey,
                Events =
                [
                    new LinkEvent
                    {
                        SourceKind = LinkSourceKind.GitHub,
                        EventKind = LinkEventKind.Issue,
                        Title = "Issue title",
                        UserName = "octocat",
                        CreatedAt = newUpdatedAt,
                        EventKey = newEventKey,
                        Body = "Issue body",
                        ResourceUrl = new Uri("https://github.com/user/repo/issues/123")
                    }
                ]
            });

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [handler],
            botClient,
            outboxStore,
            logger,
            outboxEnabled: true);

        await sut.Execute(quartzContext);

        await outboxStore.Received(1).AddRangeAndSetCursorAsync(
            subscription.Id,
            newUpdatedAt,
            newEventKey,
            Arg.Is<IReadOnlyCollection<LinkUpdate>>(updates =>
                updates.Count == 1 &&
                updates.Single().Id == subscription.Id &&
                updates.Single().Url == subscription.Url &&
                updates.Single().TgChatIds.SequenceEqual(subscription.TgChatIds)),
            Arg.Any<CancellationToken>());

        await botClient.DidNotReceive()
            .SendUpdateAsync(
                Arg.Is<LinkUpdate>(x => x.Id == subscription.Id),
                Arg.Any<CancellationToken>());

        await trackingStore.DidNotReceive()
            .SetCursorAsync(
                Arg.Any<long>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenOutboxEnabledAndHandlerThrows_SendsFailedReportViaBotClient()
    {
        var trackingStore = Substitute.For<ILinkTrackingStore>();
        var handler = Substitute.For<ILinkUpdateHandler>();
        var botClient = Substitute.For<IBotClient>();
        var outboxStore = Substitute.For<IOutboxStore>();
        var logger = Substitute.For<ILogger<LinkUpdatesJob>>();

        var subscription = new TrackedLinkSubscription { Id = 10, Url = new Uri("https://github.com/user/repo"), TgChatIds = [1001L] };

        trackingStore.GetSubscriptionsBatchAsync(null, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([subscription]);

        trackingStore.GetSubscriptionsBatchAsync(subscription.Id, DefaultBatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        handler.CanHandle(subscription.Url).Returns(true);

        handler.CheckAsync(subscription, Arg.Any<CancellationToken>())
            .Returns<Task<LinkCheckResult>>(_ => throw new HttpRequestException("boom"));

        var quartzContext = Substitute.For<IJobExecutionContext>();
        quartzContext.CancellationToken.Returns(CancellationToken.None);

        var sut = CreateSut(
            trackingStore,
            [handler],
            botClient,
            outboxStore,
            logger,
            outboxEnabled: true);

        await sut.Execute(quartzContext);

        await outboxStore.DidNotReceive().AddRangeAndSetCursorAsync(
            Arg.Any<long>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyCollection<LinkUpdate>>(),
            Arg.Any<CancellationToken>());

        await botClient.Received(1).SendUpdateAsync(
            Arg.Is<LinkUpdate>(x =>
                x.Id == 0 &&
                x.TgChatIds.SequenceEqual(new[] { 1001L }) &&
                x.Description.Contains(SystemMessageMarkers.FailedLinkReport) &&
                x.Description.Contains(subscription.Url.ToString())),
            Arg.Any<CancellationToken>());
    }

    private static LinkUpdatesJob CreateSut(
        ILinkTrackingStore trackingStore,
        IEnumerable<ILinkUpdateHandler> handlers,
        IBotClient botClient,
        IOutboxStore outboxStore,
        ILogger<LinkUpdatesJob> logger,
        int batchSize = DefaultBatchSize,
        int maxDegreeOfParallelism = DefaultMaxDegreeOfParallelism,
        bool outboxEnabled = DefaultOutboxEnabled)
    {
        var schedulingOptions = Options.Create(new LinkUpdatesSchedulingOptions { IntervalSeconds = 30, BatchSize = batchSize, MaxDegreeOfParallelism = maxDegreeOfParallelism });

        var outboxOptions = Options.Create(new OutboxOptions { Enabled = outboxEnabled, DispatchIntervalSeconds = 10, BatchSize = 100, MaxRetryCount = 3 });

        var metrics = new ScrapperMetrics();

        return new LinkUpdatesJob(
            trackingStore,
            handlers,
            botClient,
            outboxStore,
            schedulingOptions,
            outboxOptions,
            logger,
            metrics);
    }

    private static void UpdateMaxObservedConcurrency(ref int target, int candidate)
    {
        while (true)
        {
            var current = target;

            if (candidate <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref target, candidate, current) == current)
            {
                return;
            }
        }
    }
}