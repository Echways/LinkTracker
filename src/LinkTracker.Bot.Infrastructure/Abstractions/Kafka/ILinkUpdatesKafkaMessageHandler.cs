using Confluent.Kafka;

namespace LinkTracker.Bot.Infrastructure.Abstractions.Kafka;

internal interface ILinkUpdatesKafkaMessageHandler
{
    Task<bool> HandleAsync(ConsumeResult<string, byte[]> result, CancellationToken ct);
}