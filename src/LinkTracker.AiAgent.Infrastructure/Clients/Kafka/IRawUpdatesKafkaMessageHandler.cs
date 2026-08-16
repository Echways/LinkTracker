using Confluent.Kafka;
using LinkTracker.AiAgent.Application.Abstractions;

namespace LinkTracker.AiAgent.Infrastructure.Clients.Kafka;

internal interface IRawUpdatesKafkaMessageHandler
{
    Task<bool> HandleAsync(ConsumeResult<string, byte[]> result, IMessageAck ack, CancellationToken ct);
}