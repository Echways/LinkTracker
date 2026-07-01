using LinkTracker.Shared.Contracts.AiAgent;

namespace LinkTracker.AiAgent.Infrastructure.Kafka.Abstractions;

internal interface IProcessedLinkUpdateKafkaSerializer
{
    Task<byte[]> SerializeAsync(ProcessedLinkUpdate update, string topic, CancellationToken ct);
}