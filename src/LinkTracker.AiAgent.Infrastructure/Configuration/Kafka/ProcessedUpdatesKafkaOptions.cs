namespace LinkTracker.AiAgent.Infrastructure.Configuration.Kafka;

public sealed class ProcessedUpdatesKafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9094,localhost:9095,localhost:9096";
    public string Topic { get; set; } = "link.processed-updates";
}