using Confluent.Kafka;

namespace LinkTracker.AiAgent.Infrastructure.Kafka.Abstractions;

internal interface IRawUpdateDeadLetterPublisher
{
    Task PublishAsync(
        ConsumeResult<string, byte[]> sourceMessage,
        string reason,
        Exception? exception,
        CancellationToken ct);
}
