using System.Text.Json;
using Confluent.Kafka;
using LinkTracker.Bot.Infrastructure.Abstractions.Kafka;
using LinkTracker.Bot.Infrastructure.Configuration.Kafka;
using LinkTracker.Bot.Infrastructure.Models.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.Bot.Infrastructure.Clients.Kafka;

internal sealed class KafkaLinkUpdateDeadLetterPublisher(
    IProducer<Null, string> producer,
    IOptions<LinkUpdatesKafkaOptions> options,
    ILogger<KafkaLinkUpdateDeadLetterPublisher> logger) : ILinkUpdateDeadLetterPublisher
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(
        ConsumeResult<string, byte[]> sourceMessage,
        string reason,
        Exception? exception,
        CancellationToken ct)
    {
        var deadLetterMessage = new LinkUpdatesDeadLetterKafkaMessage
        {
            Payload = Convert.ToBase64String(sourceMessage.Message.Value),
            Reason = reason,
            ExceptionType = exception?.GetType().FullName,
            SourceTopic = sourceMessage.Topic,
            SourcePartition = sourceMessage.Partition.Value,
            SourceOffset = sourceMessage.Offset.Value
        };

        var payload = JsonSerializer.Serialize(deadLetterMessage, JsonSerializerOptions);

        var result = await producer.ProduceAsync(
            options.Value.DeadLetterTopic,
            new Message<Null, string> { Value = payload },
            ct);

        logger.LogWarning(
            "Kafka сообщение отправлено в DLQ. Topic={Topic}, Partition={Partition}, Offset={Offset}, Reason={Reason}",
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            reason);
    }
}