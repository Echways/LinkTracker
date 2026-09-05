using System.Text.Json;
using Confluent.Kafka;
using LinkTracker.AiAgent.Infrastructure.Configuration.Kafka;
using LinkTracker.AiAgent.Infrastructure.Kafka.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Models.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.AiAgent.Infrastructure.Clients.Kafka;

internal sealed class RawUpdatesKafkaDeadLetterPublisher(
    IProducer<string, byte[]> producer,
    IOptions<RawUpdatesKafkaOptions> options,
    ILogger<RawUpdatesKafkaDeadLetterPublisher> logger) : IRawUpdateDeadLetterPublisher
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(
        ConsumeResult<string, byte[]> sourceMessage,
        string reason,
        Exception? exception,
        CancellationToken ct)
    {
        var deadLetterMessage = new RawUpdatesDeadLetterKafkaMessage
        {
            Payload = Convert.ToBase64String(sourceMessage.Message.Value),
            Reason = reason,
            ExceptionType = exception?.GetType().FullName,
            SourceTopic = sourceMessage.Topic,
            SourcePartition = sourceMessage.Partition.Value,
            SourceOffset = sourceMessage.Offset.Value
        };

        var payload = JsonSerializer.SerializeToUtf8Bytes(deadLetterMessage, JsonSerializerOptions);

        var result = await producer.ProduceAsync(
            options.Value.DeadLetterTopic,
            new Message<string, byte[]> { Key = sourceMessage.Message.Key, Value = payload },
            ct);

        logger.LogWarning(
            "Kafka message sent to DLQ. Topic={Topic}, Partition={Partition}, Offset={Offset}, Reason={Reason}",
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            reason);
    }
}
