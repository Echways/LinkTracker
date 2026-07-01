using Confluent.Kafka;

namespace LinkTracker.Bot.Infrastructure.Abstractions.Kafka;

internal interface ILinkUpdateDeadLetterPublisher
{
    Task PublishAsync(
        ConsumeResult<string, byte[]> sourceMessage,
        string reason,
        Exception? exception,
        CancellationToken ct);
}