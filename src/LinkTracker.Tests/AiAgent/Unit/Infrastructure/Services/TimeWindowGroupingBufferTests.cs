using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.AiAgent.Infrastructure.Services;
using LinkTracker.Shared.Contracts.AiAgent;
using Microsoft.Extensions.Options;

namespace LinkTracker.Tests.AiAgent.Unit.Infrastructure.Services;

[Trait("Module", "AiAgent")]
[Trait("Category", "Unit")]
public sealed class TimeWindowGroupingBufferTests
{
    [Fact]
    public void Add_HoldsMessageUntilBufferIsFlushed()
    {
        var buffer = CreateBuffer();
        var ack = new CountingAck();

        buffer.Add(42, BuildUpdate(), ack);

        Assert.False(ack.IsCompleted);
    }

    [Fact]
    public void Flush_WhenWindowIsStillOpen_ReturnsNothing()
    {
        var buffer = CreateBuffer();

        buffer.Add(42, BuildUpdate(), new CountingAck());

        Assert.Empty(buffer.Flush());
    }

    [Fact]
    public void Flush_WhenForced_ReturnsOpenWindows()
    {
        var buffer = CreateBuffer();
        var ack = new CountingAck();

        buffer.Add(42, BuildUpdate(1), ack);
        buffer.Add(42, BuildUpdate(2), ack);

        var bucket = Assert.Single(buffer.Flush(true));

        Assert.Equal(42, bucket.ChatId);
        Assert.Equal([1L, 2L], bucket.Updates.Select(x => x.Update.Id));
        Assert.All(bucket.Updates, x => Assert.Same(ack, x.Ack));
    }

    [Fact]
    public void Flush_WhenWindowExpired_ReturnsBucket()
    {
        var buffer = CreateBuffer(1);

        buffer.Add(42, BuildUpdate(), new CountingAck());

        Thread.Sleep(20);

        Assert.Single(buffer.Flush());
    }

    [Fact]
    public void Requeue_ReturnsUpdatesToBufferWithoutCompletingMessage()
    {
        var buffer = CreateBuffer();
        var ack = new CountingAck();

        buffer.Add(42, BuildUpdate(), ack);

        var bucket = Assert.Single(buffer.Flush(true));

        buffer.Requeue(bucket);

        Assert.False(ack.IsCompleted);

        var requeued = Assert.Single(buffer.Flush(true));

        Assert.Equal(bucket.Updates[0].Update.Id, Assert.Single(requeued.Updates).Update.Id);

        foreach (var buffered in requeued.Updates)
        {
            buffered.Ack.Release();
        }

        Assert.False(ack.IsCompleted);

        ack.Release();

        Assert.True(ack.IsCompleted);
    }

    private static TimeWindowGroupingBuffer CreateBuffer(int windowMs = 60000)
    {
        return new TimeWindowGroupingBuffer(
            Options.Create(new AiAgentOptions { Grouping = new GroupingOptions { WindowMs = windowMs } }));
    }

    private static ProcessedLinkUpdate BuildUpdate(long id = 1)
    {
        return new ProcessedLinkUpdate
        {
            Id = id,
            Url = new Uri("https://github.com/user/repo"),
            Description = "Обновление",
            TgChatIds = [42]
        };
    }

    private sealed class CountingAck : IMessageAck
    {
        private int _references = 1;

        public bool IsCompleted { get; private set; }

        public void Retain()
        {
            Interlocked.Increment(ref _references);
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _references) == 0)
            {
                IsCompleted = true;
            }
        }
    }
}
