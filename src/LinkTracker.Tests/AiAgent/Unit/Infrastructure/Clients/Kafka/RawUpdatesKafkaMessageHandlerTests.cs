using System.Text;
using Confluent.Kafka;
using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Application.Telemetry.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Clients.Kafka;
using LinkTracker.AiAgent.Infrastructure.Configuration.Kafka;
using LinkTracker.AiAgent.Infrastructure.Kafka.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Kafka.Deserialization;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LinkTracker.Tests.AiAgent.Unit.Infrastructure.Clients.Kafka;

[Trait("Module", "AiAgent")]
[Trait("Category", "Unit")]
public sealed class RawUpdatesKafkaMessageHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenMessageIsValid_ProcessesAndReturnsTrue()
    {
        var processingService = Substitute.For<ILinkUpdateProcessingService>();
        var deadLetterPublisher = Substitute.For<IRawUpdateDeadLetterPublisher>();

        var sut = CreateSut(processingService, deadLetterPublisher, 3);

        var result = await sut.HandleAsync(CreateValidConsumeResult(), Substitute.For<IMessageAck>(), CancellationToken.None);

        Assert.True(result);

        await processingService.Received(1).ProcessAsync(
            Arg.Is<LinkUpdate>(u => u.Id == 42),
            Arg.Any<IMessageAck>(),
            Arg.Any<CancellationToken>());

        await deadLetterPublisher.DidNotReceive().PublishAsync(
            Arg.Any<ConsumeResult<string, byte[]>>(),
            Arg.Any<string>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenMessageIsMalformed_PublishesToDeadLetterAndReturnsTrue()
    {
        var processingService = Substitute.For<ILinkUpdateProcessingService>();
        var deadLetterPublisher = Substitute.For<IRawUpdateDeadLetterPublisher>();

        var sut = CreateSut(processingService, deadLetterPublisher, 3);

        var message = CreateConsumeResult("{ invalid json");

        var result = await sut.HandleAsync(message, Substitute.For<IMessageAck>(), CancellationToken.None);

        Assert.True(result);

        await processingService.DidNotReceive().ProcessAsync(
            Arg.Any<LinkUpdate>(),
            Arg.Any<IMessageAck>(),
            Arg.Any<CancellationToken>());

        await deadLetterPublisher.Received(1).PublishAsync(
            message,
            Arg.Any<string>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenProcessingFails_RetriesThenPublishesToDeadLetter()
    {
        var processingService = Substitute.For<ILinkUpdateProcessingService>();
        var deadLetterPublisher = Substitute.For<IRawUpdateDeadLetterPublisher>();

        processingService
            .ProcessAsync(Arg.Any<LinkUpdate>(), Arg.Any<IMessageAck>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("YandexAi failed")));

        var sut = CreateSut(processingService, deadLetterPublisher, 3);

        var message = CreateValidConsumeResult();

        var result = await sut.HandleAsync(message, Substitute.For<IMessageAck>(), CancellationToken.None);

        Assert.True(result);

        await processingService.Received(3).ProcessAsync(
            Arg.Any<LinkUpdate>(),
            Arg.Any<IMessageAck>(),
            Arg.Any<CancellationToken>());

        await deadLetterPublisher.Received(1).PublishAsync(
            message,
            "Kafka message processing retries exhausted.",
            Arg.Is<InvalidOperationException>(ex => ex.Message == "YandexAi failed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenDeadLetterPublishingFails_ReturnsFalseAndIncrementsMetric()
    {
        var processingService = Substitute.For<ILinkUpdateProcessingService>();
        var deadLetterPublisher = Substitute.For<IRawUpdateDeadLetterPublisher>();
        var metrics = Substitute.For<IAiAgentMetrics>();

        deadLetterPublisher
            .PublishAsync(
                Arg.Any<ConsumeResult<string, byte[]>>(),
                Arg.Any<string>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Kafka DLQ failed")));

        var sut = CreateSut(processingService, deadLetterPublisher, 3, metrics: metrics);

        var result = await sut.HandleAsync(CreateConsumeResult("{ invalid json"), Substitute.For<IMessageAck>(), CancellationToken.None);

        Assert.False(result);

        metrics.Received(1).IncrementKafkaDeadLetterError("link.raw-updates");
        metrics.DidNotReceive().IncrementKafkaDeadLetter(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var processingService = Substitute.For<ILinkUpdateProcessingService>();
        var deadLetterPublisher = Substitute.For<IRawUpdateDeadLetterPublisher>();

        using var cts = new CancellationTokenSource();

        processingService
            .ProcessAsync(Arg.Any<LinkUpdate>(), Arg.Any<IMessageAck>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromCanceled(cts.Token));

        var sut = CreateSut(processingService, deadLetterPublisher, 3);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.HandleAsync(CreateValidConsumeResult(), Substitute.For<IMessageAck>(), cts.Token));

        await deadLetterPublisher.DidNotReceive().PublishAsync(
            Arg.Any<ConsumeResult<string, byte[]>>(),
            Arg.Any<string>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    private static RawUpdatesKafkaMessageHandler CreateSut(
        ILinkUpdateProcessingService processingService,
        IRawUpdateDeadLetterPublisher deadLetterPublisher,
        int retryAttempts,
        int retryBackoffMilliseconds = 0,
        IAiAgentMetrics? metrics = null)
    {
        var options = Options.Create(new RawUpdatesKafkaOptions
        {
            BootstrapServers = "localhost:9092",
            Topic = "link.raw-updates",
            GroupId = "linktracker-ai-agent",
            DeadLetterTopic = "link.raw-updates-dlq",
            RetryAttempts = retryAttempts,
            RetryBackoffMilliseconds = retryBackoffMilliseconds
        });

        return new RawUpdatesKafkaMessageHandler(
            new JsonRawLinkUpdateKafkaDeserializer(),
            processingService,
            deadLetterPublisher,
            options,
            metrics ?? Substitute.For<IAiAgentMetrics>(),
            NullLogger<RawUpdatesKafkaMessageHandler>.Instance);
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
        return new ConsumeResult<string, byte[]>
        {
            Topic = "link.raw-updates",
            Partition = new Partition(0),
            Offset = new Offset(1),
            Message = new Message<string, byte[]> { Key = "key", Value = Encoding.UTF8.GetBytes(payload) }
        };
    }
}
