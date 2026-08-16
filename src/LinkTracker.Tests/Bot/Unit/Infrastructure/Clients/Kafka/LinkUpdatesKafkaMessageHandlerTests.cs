using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Bot.Application.Updates.Abstractions;
using LinkTracker.Bot.Infrastructure.Abstractions.Kafka;
using LinkTracker.Bot.Infrastructure.Clients.Kafka;
using LinkTracker.Bot.Infrastructure.Configuration.Kafka;
using LinkTracker.Bot.Infrastructure.Kafka.Deserialization;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LinkTracker.Tests.Bot.Unit.Infrastructure.Clients.Kafka;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class LinkUpdatesKafkaMessageHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenMessageIsValid_NotifiesAndReturnsTrue()
    {
        var notifier = Substitute.For<ILinkUpdateNotifier>();
        var deadLetterPublisher = Substitute.For<ILinkUpdateDeadLetterPublisher>();

        var sut = CreateSut(
            notifier,
            deadLetterPublisher,
            3);

        var message = CreateConsumeResult(
            """
            {
              "id": 42,
              "url": "https://github.com/user/repo",
              "description": "Repository updated",
              "tgChatIds": [123]
            }
            """);

        var result = await sut.HandleAsync(message, CancellationToken.None);

        Assert.True(result);

        await notifier.Received(1).NotifyAsync(
            Arg.Is<LinkUpdate>(update =>
                update.Id == 42
                && update.Url == new Uri("https://github.com/user/repo")
                && update.Description == "Repository updated"
                && update.TgChatIds.SequenceEqual(new[] { 123L })),
            Arg.Any<CancellationToken>());
        await deadLetterPublisher.DidNotReceive().PublishAsync(
            Arg.Any<ConsumeResult<string, byte[]>>(),
            Arg.Any<string>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenMessageIsInvalid_PublishesToDeadLetterAndReturnsTrue()
    {
        var notifier = Substitute.For<ILinkUpdateNotifier>();
        var deadLetterPublisher = Substitute.For<ILinkUpdateDeadLetterPublisher>();

        var sut = CreateSut(
            notifier,
            deadLetterPublisher,
            3);

        var message = CreateConsumeResult("{ invalid json");

        var result = await sut.HandleAsync(message, CancellationToken.None);

        Assert.True(result);

        await notifier.DidNotReceive().NotifyAsync(
            Arg.Any<LinkUpdate>(),
            Arg.Any<CancellationToken>());

        await deadLetterPublisher.Received(1).PublishAsync(
            message,
            Arg.Is<string>(reason => reason.StartsWith("Kafka сообщение не удалось десериализовать:")),
            Arg.Any<JsonException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierFailsOnce_RetriesAndReturnsTrue()
    {
        var notifier = Substitute.For<ILinkUpdateNotifier>();
        var deadLetterPublisher = Substitute.For<ILinkUpdateDeadLetterPublisher>();

        var attempts = 0;

        notifier
            .NotifyAsync(Arg.Any<LinkUpdate>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;

                return attempts == 1
                    ? Task.FromException(new InvalidOperationException("Telegram failed"))
                    : Task.CompletedTask;
            });

        var sut = CreateSut(
            notifier,
            deadLetterPublisher,
            3);

        var message = CreateValidConsumeResult();

        var result = await sut.HandleAsync(message, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(2, attempts);

        await notifier.Received(2).NotifyAsync(
            Arg.Any<LinkUpdate>(),
            Arg.Any<CancellationToken>());

        await deadLetterPublisher.DidNotReceive().PublishAsync(
            Arg.Any<ConsumeResult<string, byte[]>>(),
            Arg.Any<string>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierAlwaysFails_PublishesToDeadLetterAndReturnsTrue()
    {
        var notifier = Substitute.For<ILinkUpdateNotifier>();
        var deadLetterPublisher = Substitute.For<ILinkUpdateDeadLetterPublisher>();

        notifier
            .NotifyAsync(Arg.Any<LinkUpdate>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Telegram failed")));

        var sut = CreateSut(
            notifier,
            deadLetterPublisher,
            3);

        var message = CreateValidConsumeResult();

        var result = await sut.HandleAsync(message, CancellationToken.None);

        Assert.True(result);

        await notifier.Received(3).NotifyAsync(
            Arg.Any<LinkUpdate>(),
            Arg.Any<CancellationToken>());

        await deadLetterPublisher.Received(1).PublishAsync(
            message,
            "Исчерпаны попытки обработки Kafka сообщения.",
            Arg.Is<InvalidOperationException>(ex => ex.Message == "Telegram failed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenDeadLetterPublishingFails_ReturnsFalse()
    {
        var notifier = Substitute.For<ILinkUpdateNotifier>();
        var deadLetterPublisher = Substitute.For<ILinkUpdateDeadLetterPublisher>();

        deadLetterPublisher
            .PublishAsync(
                Arg.Any<ConsumeResult<string, byte[]>>(),
                Arg.Any<string>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Kafka DLQ failed")));

        var sut = CreateSut(
            notifier,
            deadLetterPublisher,
            3);

        var message = CreateConsumeResult("{ invalid json");

        var result = await sut.HandleAsync(message, CancellationToken.None);

        Assert.False(result);

        await notifier.DidNotReceive().NotifyAsync(
            Arg.Any<LinkUpdate>(),
            Arg.Any<CancellationToken>());

        await deadLetterPublisher.Received(1).PublishAsync(
            message,
            Arg.Any<string>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenDeadLetterPublishingFails_IncrementsDeadLetterErrorMetric()
    {
        var notifier = Substitute.For<ILinkUpdateNotifier>();
        var deadLetterPublisher = Substitute.For<ILinkUpdateDeadLetterPublisher>();
        var metrics = Substitute.For<IBotMetrics>();

        deadLetterPublisher
            .PublishAsync(
                Arg.Any<ConsumeResult<string, byte[]>>(),
                Arg.Any<string>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Kafka DLQ failed")));

        var sut = CreateSut(
            notifier,
            deadLetterPublisher,
            3,
            metrics: metrics);

        var result = await sut.HandleAsync(CreateConsumeResult("{ invalid json"), CancellationToken.None);

        Assert.False(result);

        metrics.Received(1).IncrementKafkaDeadLetterError("link.processed-updates");
        metrics.Received(1).IncrementError("kafka_dead_letter", "link.processed-updates", "publish_failed");
        metrics.DidNotReceive().IncrementKafkaDeadLetter(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_WhenDeadLetterPublishingSucceeds_IncrementsDeadLetterMetric()
    {
        var notifier = Substitute.For<ILinkUpdateNotifier>();
        var deadLetterPublisher = Substitute.For<ILinkUpdateDeadLetterPublisher>();
        var metrics = Substitute.For<IBotMetrics>();

        var sut = CreateSut(
            notifier,
            deadLetterPublisher,
            3,
            metrics: metrics);

        var result = await sut.HandleAsync(CreateConsumeResult("{ invalid json"), CancellationToken.None);

        Assert.True(result);

        metrics.Received(1).IncrementKafkaDeadLetter("link.processed-updates");
        metrics.DidNotReceive().IncrementKafkaDeadLetterError(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var notifier = Substitute.For<ILinkUpdateNotifier>();
        var deadLetterPublisher = Substitute.For<ILinkUpdateDeadLetterPublisher>();

        using var cts = new CancellationTokenSource();

        notifier
            .NotifyAsync(Arg.Any<LinkUpdate>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromCanceled(cts.Token));

        var sut = CreateSut(
            notifier,
            deadLetterPublisher,
            3);

        var message = CreateValidConsumeResult();

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.HandleAsync(message, cts.Token));

        await deadLetterPublisher.DidNotReceive().PublishAsync(
            Arg.Any<ConsumeResult<string, byte[]>>(),
            Arg.Any<string>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    private static LinkUpdatesKafkaMessageHandler CreateSut(
        ILinkUpdateNotifier notifier,
        ILinkUpdateDeadLetterPublisher deadLetterPublisher,
        int retryAttempts,
        int retryBackoffMilliseconds = 0,
        IBotMetrics? metrics = null)
    {
        var options = Options.Create(new LinkUpdatesKafkaOptions
        {
            Topic = "link.processed-updates",
            DeadLetterTopic = "link.processed-updates-dlq",
            GroupId = "linktracker-bot",
            BootstrapServers = "localhost:9092",
            RetryAttempts = retryAttempts,
            RetryBackoffMilliseconds = retryBackoffMilliseconds
        });

        return new LinkUpdatesKafkaMessageHandler(
            new JsonLinkUpdateKafkaDeserializer(),
            new KafkaLinkUpdateMessageParser(),
            deadLetterPublisher,
            notifier,
            options,
            metrics ?? Substitute.For<IBotMetrics>(),
            NullLogger<LinkUpdatesKafkaMessageHandler>.Instance);
    }

    private static ConsumeResult<string, byte[]> CreateValidConsumeResult()
    {
        return CreateConsumeResult(
            """
            {
              "id": 42,
              "url": "https://github.com/user/repo",
              "description": "Repository updated",
              "tgChatIds": [123]
            }
            """);
    }

    private static ConsumeResult<string, byte[]> CreateConsumeResult(string payload)
    {
        return new ConsumeResult<string, byte[]> { Topic = "link.processed-updates", Partition = new Partition(0), Offset = new Offset(1), Message = new Message<string, byte[]> { Key = "key", Value = Encoding.UTF8.GetBytes(payload) } };
    }
}