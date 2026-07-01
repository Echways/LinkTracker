using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Bot.Infrastructure.Kafka.Abstractions;

internal interface ILinkUpdateKafkaDeserializer
{
    Task<LinkUpdate?> DeserializeAsync(byte[] payload, string topic, CancellationToken ct);
}