using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.Shared.Contracts.AiAgent;
using Microsoft.Extensions.Options;

namespace LinkTracker.AiAgent.Infrastructure.Services;

internal sealed class TimeWindowGroupingBuffer(IOptions<AiAgentOptions> options) : IGroupingBuffer
{
    private readonly Dictionary<long, (List<ProcessedLinkUpdate> Updates, DateTimeOffset WindowStart)> _buckets = [];
    private readonly List<(long ChatId, List<ProcessedLinkUpdate> Updates)> _evicted = [];
    private readonly Lock _lock = new();

    public void Add(long tgChatId, ProcessedLinkUpdate update)
    {
        var windowMs = options.Value.Grouping.WindowMs;
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            if (_buckets.TryGetValue(tgChatId, out var bucket))
            {
                var elapsed = (now - bucket.WindowStart).TotalMilliseconds;

                if (elapsed <= windowMs)
                {
                    bucket.Updates.Add(update);
                    return;
                }

                _evicted.Add((tgChatId, bucket.Updates));
            }

            _buckets[tgChatId] = ([update], now);
        }
    }

    public IReadOnlyList<(long ChatId, IReadOnlyList<ProcessedLinkUpdate> Updates)> Flush()
    {
        var windowMs = options.Value.Grouping.WindowMs;
        var now = DateTimeOffset.UtcNow;
        var result = new List<(long, IReadOnlyList<ProcessedLinkUpdate>)>();

        lock (_lock)
        {
            foreach (var (chatId, updates) in _evicted)
            {
                result.Add((chatId, updates.AsReadOnly()));
            }

            _evicted.Clear();

            var expiredKeys = _buckets
                .Where(kv => (now - kv.Value.WindowStart).TotalMilliseconds > windowMs)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                result.Add((key, _buckets[key].Updates.AsReadOnly()));
                _buckets.Remove(key);
            }
        }

        return result;
    }
}