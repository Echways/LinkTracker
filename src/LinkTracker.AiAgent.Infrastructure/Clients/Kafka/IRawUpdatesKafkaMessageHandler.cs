using Confluent.Kafka;

namespace LinkTracker.AiAgent.Infrastructure.Clients.Kafka;

internal interface IRawUpdatesKafkaMessageHandler
{
    Task<bool> HandleAsync(ConsumeResult<string, byte[]> result, CancellationToken ct);
}