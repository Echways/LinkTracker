using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.AiAgent.Infrastructure.Services;
using LinkTracker.Shared.Contracts.AiAgent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LinkTracker.Tests.AiAgent.Unit.Infrastructure.Services;

[Trait("Module", "AiAgent")]
[Trait("Category", "Unit")]
public sealed class GroupingFlushJobTests
{
    private readonly IGroupingBuffer _buffer = Substitute.For<IGroupingBuffer>();
    private readonly IProcessedUpdatePublisher _publisher = Substitute.For<IProcessedUpdatePublisher>();

    [Fact]
    public async Task StopAsync_PublishesWindowsThatDidNotCloseYet()
    {
        var ack = Substitute.For<IMessageAck>();
        _buffer.Flush(true).Returns([new GroupingBucket(42, [new BufferedLinkUpdate(BuildUpdate(), ack)])]);

        await CreateJob().StopAsync(CancellationToken.None);

        await _publisher.Received(1).PublishAsync(
            Arg.Is<ProcessedLinkUpdate>(u => u.Id == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAsync_WhenPublished_ReleasesSourceMessages()
    {
        var ack = Substitute.For<IMessageAck>();
        _buffer.Flush(true).Returns([new GroupingBucket(42, [new BufferedLinkUpdate(BuildUpdate(), ack)])]);

        await CreateJob().StopAsync(CancellationToken.None);

        ack.Received(1).Release();
        _buffer.DidNotReceive().Requeue(Arg.Any<GroupingBucket>());
    }

    [Fact]
    public async Task StopAsync_WhenPublishingFails_KeepsMessageUnacknowledgedAndRequeuesWindow()
    {
        var ack = Substitute.For<IMessageAck>();
        var bucket = new GroupingBucket(42, [new BufferedLinkUpdate(BuildUpdate(), ack)]);

        _buffer.Flush(true).Returns([bucket]);

        _publisher
            .PublishAsync(Arg.Any<ProcessedLinkUpdate>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Kafka down")));

        await CreateJob().StopAsync(CancellationToken.None);

        ack.DidNotReceive().Release();
        _buffer.Received(1).Requeue(bucket);
    }

    [Fact]
    public async Task StopAsync_PublishesHighPriorityWindowsFirst()
    {
        var published = new List<long>();

        _publisher
            .PublishAsync(Arg.Any<ProcessedLinkUpdate>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                published.Add(call.Arg<ProcessedLinkUpdate>().Id);
                return Task.CompletedTask;
            });

        _buffer.Flush(true).Returns(
        [
            new GroupingBucket(1, [new BufferedLinkUpdate(BuildUpdate(10, LinkUpdatePriority.Low), Substitute.For<IMessageAck>())]),
            new GroupingBucket(2, [new BufferedLinkUpdate(BuildUpdate(20, LinkUpdatePriority.High), Substitute.For<IMessageAck>())])
        ]);

        await CreateJob().StopAsync(CancellationToken.None);

        Assert.Equal([20L, 10L], published);
    }

    private GroupingFlushJob CreateJob()
    {
        return new GroupingFlushJob(
            _buffer,
            new WindowLinkUpdateGrouper(),
            _publisher,
            Options.Create(new AiAgentOptions()),
            NullLogger<GroupingFlushJob>.Instance);
    }

    private static ProcessedLinkUpdate BuildUpdate(
        long id = 1,
        LinkUpdatePriority priority = LinkUpdatePriority.Medium)
    {
        return new ProcessedLinkUpdate
        {
            Id = id,
            Url = new Uri("https://github.com/user/repo"),
            Description = "Обновление",
            TgChatIds = [42],
            Priority = priority
        };
    }
}
