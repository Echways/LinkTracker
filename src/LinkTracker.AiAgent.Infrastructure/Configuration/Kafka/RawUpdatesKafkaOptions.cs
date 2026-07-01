namespace LinkTracker.AiAgent.Infrastructure.Configuration.Kafka;

public sealed class RawUpdatesKafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9094,localhost:9095,localhost:9096";
    public string Topic { get; set; } = "link.raw-updates";
    public string GroupId { get; set; } = "linktracker-ai-agent";
}