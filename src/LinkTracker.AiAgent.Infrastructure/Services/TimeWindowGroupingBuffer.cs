using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.Shared.Contracts.AiAgent;
using Microsoft.Extensions.Options;

namespace LinkTracker.AiAgent.Infrastructure.Services;

internal sealed class TimeWindowGroupingBuffer(IOptions<AiAgentOptions> options) : IGroupingBuffer
{
    private readonly Dictionary<long, (List<BufferedLinkUpdate> Updates, DateTimeOffset WindowStart)> _buckets = [];
    private readonly List<GroupingBucket> _evicted = [];
    private readonly Lock _lock = new();

    public void Add(long tgChatId, ProcessedLinkUpdate update, IMessageAck ack)
    {
        var windowMs = options.Value.Grouping.WindowMs;
        var now = DateTimeOffset.UtcNow;

        ack.Retain();

        var buffered = new BufferedLinkUpdate(update, ack);

        lock (_lock)
        {
            if (_buckets.TryGetValue(tgChatId, out var bucket))
            {
                var elapsed = (now - bucket.WindowStart).TotalMilliseconds;

                if (elapsed <= windowMs)
                {
                    bucket.Updates.Add(buffered);
                    return;
                }

                _evicted.Add(new GroupingBucket(tgChatId, bucket.Updates));
            }

            _buckets[tgChatId] = ([buffered], now);
        }
    }

    public IReadOnlyList<GroupingBucket> Flush(bool force = false)
    {
        var windowMs = options.Value.Grouping.WindowMs;
        var now = DateTimeOffset.UtcNow;
        var result = new List<GroupingBucket>();

        lock (_lock)
        {
            result.AddRange(_evicted);
            _evicted.Clear();

            var expiredKeys = _buckets
                .Where(kv => force || (now - kv.Value.WindowStart).TotalMilliseconds > windowMs)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                result.Add(new GroupingBucket(key, _buckets[key].Updates));
                _buckets.Remove(key);
            }
        }

        return result;
    }

    public void Requeue(GroupingBucket bucket)
    {
        foreach (var buffered in bucket.Updates)
        {
            Add(bucket.ChatId, buffered.Update, buffered.Ack);
            buffered.Ack.Release();
        }
    }
}
