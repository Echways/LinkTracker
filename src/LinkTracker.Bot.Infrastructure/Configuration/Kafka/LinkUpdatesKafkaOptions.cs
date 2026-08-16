using LinkTracker.Shared.Infrastructure;

namespace LinkTracker.Bot.Infrastructure.Configuration.Kafka;

public sealed class LinkUpdatesKafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9094,localhost:9095,localhost:9096";
    public string Topic { get; set; } = "link.processed-updates";
    public string GroupId { get; set; } = "linktracker-bot";
    public string DeadLetterTopic { get; set; } = "link.processed-updates-dlq";
    public int RetryAttempts { get; set; } = 3;
    public int RetryBackoffMilliseconds { get; set; } = 500;
    public KafkaSerializationKind Serialization { get; set; } = KafkaSerializationKind.Json;
    public string SchemaRegistryUrl { get; set; } = "http://localhost:8071";
}