using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.AiAgent.Infrastructure.Kafka.Abstractions;

internal interface IRawLinkUpdateKafkaDeserializer
{
    Task<LinkUpdate?> DeserializeAsync(byte[] payload, string topic, CancellationToken ct);
}