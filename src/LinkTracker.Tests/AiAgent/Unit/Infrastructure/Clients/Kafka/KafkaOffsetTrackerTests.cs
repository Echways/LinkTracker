using Confluent.Kafka;
using LinkTracker.AiAgent.Infrastructure.Clients.Kafka;

namespace LinkTracker.Tests.AiAgent.Unit.Infrastructure.Clients.Kafka;

[Trait("Module", "AiAgent")]
[Trait("Category", "Unit")]
public sealed class KafkaOffsetTrackerTests
{
    private const string Topic = "link.raw-updates";

    [Fact]
    public void TakeCommittableOffsets_WhenMessageNotReleased_ReturnsNothing()
    {
        var tracker = new KafkaOffsetTracker();

        tracker.Track(CreateResult(0));

        Assert.Empty(tracker.TakeCommittableOffsets());
    }

    [Fact]
    public void TakeCommittableOffsets_WhenMessageReleased_ReturnsNextOffset()
    {
        var tracker = new KafkaOffsetTracker();

        tracker.Track(CreateResult(7)).Release();

        var offset = Assert.Single(tracker.TakeCommittableOffsets());

        Assert.Equal(8, offset.Offset.Value);
        Assert.Equal(Topic, offset.Topic);
    }

    [Fact]
    public void TakeCommittableOffsets_WhenBufferStillHoldsMessage_ReturnsNothing()
    {
        var tracker = new KafkaOffsetTracker();
        var ack = tracker.Track(CreateResult(0));

        // Копия обновления лежит в буфере группировки.
        ack.Retain();
        ack.Release();

        Assert.Empty(tracker.TakeCommittableOffsets());

        ack.Release();

        Assert.Equal(1, Assert.Single(tracker.TakeCommittableOffsets()).Offset.Value);
    }

    [Fact]
    public void TakeCommittableOffsets_WhenEarlierMessageStillInFlight_StopsBeforeIt()
    {
        var tracker = new KafkaOffsetTracker();

        var first = tracker.Track(CreateResult(0));
        tracker.Track(CreateResult(1)).Release();
        tracker.Track(CreateResult(2)).Release();

        Assert.Empty(tracker.TakeCommittableOffsets());

        first.Release();

        Assert.Equal(3, Assert.Single(tracker.TakeCommittableOffsets()).Offset.Value);
    }

    [Fact]
    public void TakeCommittableOffsets_WhenNoNewProgress_ReturnsNothingOnSecondCall()
    {
        var tracker = new KafkaOffsetTracker();

        tracker.Track(CreateResult(0)).Release();

        Assert.Single(tracker.TakeCommittableOffsets());
        Assert.Empty(tracker.TakeCommittableOffsets());
    }

    [Fact]
    public void TakeCommittableOffsets_TracksPartitionsIndependently()
    {
        var tracker = new KafkaOffsetTracker();

        tracker.Track(CreateResult(5, partition: 0));
        tracker.Track(CreateResult(9, partition: 1)).Release();

        var offset = Assert.Single(tracker.TakeCommittableOffsets());

        Assert.Equal(1, offset.Partition.Value);
        Assert.Equal(10, offset.Offset.Value);
    }

    [Fact]
    public void TakeCommittableOffsets_AfterRevoke_IgnoresStaleAcks()
    {
        var tracker = new KafkaOffsetTracker();
        var ack = tracker.Track(CreateResult(3));

        tracker.Forget([new TopicPartition(Topic, new Partition(0))]);

        ack.Release();

        Assert.Empty(tracker.TakeCommittableOffsets());
    }

    private static ConsumeResult<string, byte[]> CreateResult(long offset, int partition = 0)
    {
        return new ConsumeResult<string, byte[]>
        {
            Topic = Topic,
            Partition = new Partition(partition),
            Offset = new Offset(offset),
            Message = new Message<string, byte[]> { Key = "key", Value = [] }
        };
    }
}
