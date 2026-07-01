using Confluent.Kafka;
using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Configuration.Kafka;
using LinkTracker.AiAgent.Infrastructure.Kafka.Abstractions;
using LinkTracker.Shared.Contracts.AiAgent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.AiAgent.Infrastructure.Clients.Kafka;

internal sealed class ProcessedUpdatesKafkaPublisher(
    IProducer<string, byte[]> producer,
    IProcessedLinkUpdateKafkaSerializer serializer,
    IOptions<ProcessedUpdatesKafkaOptions> options,
    ILogger<ProcessedUpdatesKafkaPublisher> logger) : IProcessedUpdatePublisher
{
    public async Task PublishAsync(ProcessedLinkUpdate update, CancellationToken ct)
    {
        var topic = options.Value.Topic;
        var payload = await serializer.SerializeAsync(update, topic, ct);

        var message = new Message<string, byte[]> { Key = update.Id.ToString(), Value = payload };

        var result = await producer.ProduceAsync(topic, message, ct);

        logger.LogInformation(
            "Kafka: обновление опубликовано. Topic={Topic}, Partition={Partition}, Offset={Offset}, UpdateId={UpdateId}",
            result.Topic, result.Partition.Value, result.Offset.Value, update.Id);
    }
}