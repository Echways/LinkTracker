using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Scrapper.Infrastructure.Kafka.Abstractions;

internal interface ILinkUpdateKafkaSerializer
{
    Task<byte[]> SerializeAsync(LinkUpdate update, string topic, CancellationToken ct);
}