using Confluent.Kafka;
using LinkTracker.AiAgent.Application.Abstractions;

namespace LinkTracker.AiAgent.Infrastructure.Clients.Kafka;

internal sealed class KafkaOffsetTracker
{
    private readonly Dictionary<TopicPartition, PartitionState> _partitions = [];
    private readonly Lock _lock = new();
    private long _generation;

    public IMessageAck Track(ConsumeResult<string, byte[]> result)
    {
        var partition = result.TopicPartition;
        var offset = result.Offset.Value;

        lock (_lock)
        {
            if (!_partitions.TryGetValue(partition, out var state))
            {
                state = new PartitionState(_generation) { LastCommitted = offset - 1 };
                _partitions[partition] = state;
            }

            state.InFlight.Add(offset);
            state.MaxSeen = Math.Max(state.MaxSeen, offset);

            return new MessageAck(this, partition, state.Generation, offset);
        }
    }

    public IReadOnlyList<TopicPartitionOffset> TakeCommittableOffsets()
    {
        lock (_lock)
        {
            var result = new List<TopicPartitionOffset>();

            foreach (var (partition, state) in _partitions)
            {
                var watermark = state.InFlight.Count == 0 ? state.MaxSeen : state.InFlight.Min - 1;

                if (watermark < 0 || watermark <= state.LastCommitted)
                {
                    continue;
                }

                state.LastCommitted = watermark;
                result.Add(new TopicPartitionOffset(partition, new Offset(watermark + 1)));
            }

            return result;
        }
    }

    public void Forget(IEnumerable<TopicPartition> partitions)
    {
        lock (_lock)
        {
            _generation++;

            foreach (var partition in partitions)
            {
                _partitions.Remove(partition);
            }
        }
    }

    private void Complete(TopicPartition partition, long generation, long offset)
    {
        lock (_lock)
        {
            if (_partitions.TryGetValue(partition, out var state) && state.Generation == generation)
            {
                state.InFlight.Remove(offset);
            }
        }
    }

    private sealed class PartitionState(long generation)
    {
        public SortedSet<long> InFlight { get; } = [];

        public long MaxSeen { get; set; } = -1;

        public long LastCommitted { get; set; } = -1;

        public long Generation { get; } = generation;
    }

    private sealed class MessageAck(
        KafkaOffsetTracker tracker,
        TopicPartition partition,
        long generation,
        long offset) : IMessageAck
    {
        private int _references = 1;

        public void Retain()
        {
            Interlocked.Increment(ref _references);
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _references) == 0)
            {
                tracker.Complete(partition, generation, offset);
            }
        }
    }
}
