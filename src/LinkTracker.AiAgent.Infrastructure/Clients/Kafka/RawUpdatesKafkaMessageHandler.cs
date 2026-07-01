using Confluent.Kafka;
using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Kafka.Abstractions;
using Microsoft.Extensions.Logging;

namespace LinkTracker.AiAgent.Infrastructure.Clients.Kafka;

internal sealed class RawUpdatesKafkaMessageHandler(
    IRawLinkUpdateKafkaDeserializer deserializer,
    ILinkUpdateProcessingService processingService,
    ILogger<RawUpdatesKafkaMessageHandler> logger) : IRawUpdatesKafkaMessageHandler
{
    public async Task<bool> HandleAsync(ConsumeResult<string, byte[]> result, CancellationToken ct)
    {
        try
        {
            var update = await deserializer.DeserializeAsync(result.Message.Value, result.Topic, ct);

            if (update is null)
            {
                logger.LogWarning(
                    "Kafka сообщение десериализовалось в null. Topic={Topic}, Offset={Offset}",
                    result.Topic, result.Offset.Value);

                return true;
            }

            await processingService.ProcessAsync(update, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка обработки Kafka сообщения. Topic={Topic}, Partition={Partition}, Offset={Offset}",
                result.Topic, result.Partition.Value, result.Offset.Value);

            return true;
        }
    }
}