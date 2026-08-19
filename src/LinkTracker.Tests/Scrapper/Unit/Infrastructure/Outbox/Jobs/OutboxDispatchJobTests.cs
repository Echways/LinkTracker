using System.Net;
using LinkTracker.Scrapper.Infrastructure.Clients.Bot;
using LinkTracker.Scrapper.Infrastructure.Configuration.Bot;
using LinkTracker.Scrapper.Infrastructure.Outbox.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Outbox.Configuration;
using LinkTracker.Scrapper.Infrastructure.Outbox.Jobs;
using LinkTracker.Scrapper.Infrastructure.Telemetry;
using LinkTracker.Scrapper.Infrastructure.Outbox.Models;
using LinkTracker.Shared.Contracts.Bot;
using LinkTracker.Shared.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Quartz;

namespace LinkTracker.Tests.Scrapper.Unit.Infrastructure.Outbox.Jobs;

[Trait("Module", "Scrapper")]
[Trait("Category", "Unit")]
public sealed class OutboxDispatchJobTests
{
    [Fact]
    public async Task Execute_WhenMessagesDoNotExist_DoesNothing()
    {
        var outboxStore = Substitute.For<IOutboxStore>();

        outboxStore.ClaimUnprocessedBatchAsync(100, 3, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var sut = CreateSut(
            outboxStore,
            CreateTransportClient(TransportKind.Http, _ => Task.CompletedTask));

        var context = CreateContext();

        await sut.Execute(context);

        await outboxStore.DidNotReceive()
            .MarkProcessedAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());

        await outboxStore.DidNotReceive()
            .MarkFailedAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenTransportSucceeds_MarksMessageProcessed()
    {
        var outboxStore = Substitute.For<IOutboxStore>();
        var message = CreateOutboxMessage();

        outboxStore.ClaimUnprocessedBatchAsync(100, 3, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns([message]);

        var sut = CreateSut(
            outboxStore,
            CreateTransportClient(TransportKind.Http, _ => Task.CompletedTask));

        var context = CreateContext();

        await sut.Execute(context);

        await outboxStore.Received(1)
            .MarkProcessedAsync(message.Id, Arg.Any<CancellationToken>());

        await outboxStore.DidNotReceive()
            .MarkFailedAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenHttpTransportUnavailable_FallsBackToKafkaAndMarksMessageProcessed()
    {
        var outboxStore = Substitute.For<IOutboxStore>();
        var message = CreateOutboxMessage();

        outboxStore.ClaimUnprocessedBatchAsync(100, 3, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns([message]);

        var kafkaWasCalled = false;

        var sut = CreateSut(
            outboxStore,
            CreateTransportClient(
                TransportKind.Http,
                _ => Task.FromException(new BotClientException(HttpStatusCode.ServiceUnavailable, "HTTP is down"))),
            CreateTransportClient(
                TransportKind.Kafka,
                _ =>
                {
                    kafkaWasCalled = true;
                    return Task.CompletedTask;
                }));

        var context = CreateContext();

        await sut.Execute(context);

        Assert.True(kafkaWasCalled);

        await outboxStore.Received(1)
            .MarkProcessedAsync(message.Id, Arg.Any<CancellationToken>());

        await outboxStore.DidNotReceive()
            .MarkFailedAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenNonRetriableHttpErrorOccurs_MarksMessageFailed()
    {
        var outboxStore = Substitute.For<IOutboxStore>();
        var message = CreateOutboxMessage();

        outboxStore.ClaimUnprocessedBatchAsync(100, 3, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns([message]);

        var sut = CreateSut(
            outboxStore,
            CreateTransportClient(
                TransportKind.Http,
                _ => Task.FromException(new BotClientException(HttpStatusCode.BadRequest, "invalid update"))),
            CreateTransportClient(TransportKind.Kafka, _ => Task.CompletedTask));

        var context = CreateContext();

        await sut.Execute(context);

        await outboxStore.DidNotReceive()
            .MarkProcessedAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());

        await outboxStore.Received(1)
            .MarkFailedAsync(
                message.Id,
                Arg.Is<string>(error => error.Contains("invalid update")),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenCancellationRequested_ThrowsAndDoesNotMarkFailed()
    {
        var outboxStore = Substitute.For<IOutboxStore>();
        var message = CreateOutboxMessage();

        outboxStore.ClaimUnprocessedBatchAsync(100, 3, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns([message]);

        using var cts = new CancellationTokenSource();

        var sut = CreateSut(
            outboxStore,
            CreateTransportClient(
                TransportKind.Http,
                token => Task.FromCanceled(token)));

        await cts.CancelAsync();

        var context = CreateContext(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.Execute(context));

        await outboxStore.DidNotReceive()
            .MarkProcessedAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());

        await outboxStore.DidNotReceive()
            .MarkFailedAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static OutboxDispatchJob CreateSut(
        IOutboxStore outboxStore,
        params IBotTransportClient[] transportClients)
    {
        var botOptions = Options.Create(new BotOptions { Transport = TransportKind.Http });

        var outboxOptions = Options.Create(new OutboxOptions { Enabled = true, DispatchIntervalSeconds = 10, BatchSize = 100, MaxRetryCount = 3, LockSeconds = 60 });

        var botClient = new FallbackBotClient(
            transportClients,
            botOptions,
            NullLogger<FallbackBotClient>.Instance);

        return new OutboxDispatchJob(
            outboxStore,
            botClient,
            outboxOptions,
            new ScrapperMetrics(),
            NullLogger<OutboxDispatchJob>.Instance);
    }

    private static IBotTransportClient CreateTransportClient(
        TransportKind transport,
        Func<CancellationToken, Task> sendAsync)
    {
        return new StubBotTransportClient(transport, sendAsync);
    }

    private static IJobExecutionContext CreateContext(CancellationToken ct = default)
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(ct);

        return context;
    }

    private static OutboxMessage CreateOutboxMessage()
    {
        return new OutboxMessage { Id = 1, Payload = new LinkUpdate { Id = 10, Url = new Uri("https://github.com/user/repo"), Description = "Repository updated", TgChatIds = [1001] }, CreatedAt = DateTimeOffset.UtcNow, RetryCount = 0 };
    }

    private sealed class StubBotTransportClient(
        TransportKind transport,
        Func<CancellationToken, Task> sendAsync) : IBotTransportClient
    {
        public TransportKind Transport { get; } = transport;

        public Task SendUpdateAsync(LinkUpdate update, CancellationToken ct = default)
        {
            return sendAsync(ct);
        }
    }
}