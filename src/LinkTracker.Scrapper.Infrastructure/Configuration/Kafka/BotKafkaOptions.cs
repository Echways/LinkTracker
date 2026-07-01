using LinkTracker.Shared.Infrastructure;

namespace LinkTracker.Scrapper.Infrastructure.Configuration.Kafka;

public sealed class BotKafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9094,localhost:9095,localhost:9096";
    public string Topic { get; set; } = "link.raw-updates";
    public KafkaSerializationKind Serialization { get; set; } = KafkaSerializationKind.Json;
    public string SchemaRegistryUrl { get; set; } = "http://localhost:8071";
}