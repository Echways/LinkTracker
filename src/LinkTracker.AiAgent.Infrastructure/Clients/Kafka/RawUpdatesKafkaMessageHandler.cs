using Confluent.Kafka;
using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Application.Telemetry.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Configuration.Kafka;
using LinkTracker.AiAgent.Infrastructure.Kafka.Abstractions;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.AiAgent.Infrastructure.Clients.Kafka;

internal sealed class RawUpdatesKafkaMessageHandler(
    IRawLinkUpdateKafkaDeserializer deserializer,
    ILinkUpdateProcessingService processingService,
    IRawUpdateDeadLetterPublisher deadLetterPublisher,
    IOptions<RawUpdatesKafkaOptions> kafkaOptions,
    IAiAgentMetrics metrics,
    ILogger<RawUpdatesKafkaMessageHandler> logger) : IRawUpdatesKafkaMessageHandler
{
    public async Task<bool> HandleAsync(
        ConsumeResult<string, byte[]> result,
        IMessageAck ack,
        CancellationToken ct)
    {
        LinkUpdate? update;

        try
        {
            update = await deserializer.DeserializeAsync(result.Message.Value, result.Topic, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await TryPublishToDeadLetterAsync(
                result,
                $"Failed to deserialize Kafka message: {ex.Message}",
                ex,
                ct);
        }

        if (update is null)
        {
            return await TryPublishToDeadLetterAsync(
                result,
                "Kafka message deserialized to null.",
                null,
                ct);
        }

        var processingError = await TryProcessWithRetriesAsync(update, ack, ct);

        if (processingError is not null)
        {
            return await TryPublishToDeadLetterAsync(
                result,
                "Kafka message processing retries exhausted.",
                processingError,
                ct);
        }

        logger.LogInformation(
            "Kafka message processed. Topic={Topic}, Partition={Partition}, Offset={Offset}, UpdateId={UpdateId}",
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            update.Id);

        return true;
    }

    private async Task<Exception?> TryProcessWithRetriesAsync(
        LinkUpdate update,
        IMessageAck ack,
        CancellationToken ct)
    {
        var attempts = Math.Max(1, kafkaOptions.Value.RetryAttempts);
        var backoff = TimeSpan.FromMilliseconds(Math.Max(0, kafkaOptions.Value.RetryBackoffMilliseconds));

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await processingService.ProcessAsync(update, ack, ct);
                return null;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < attempts)
            {
                logger.LogWarning(
                    ex,
                    "Failed to process Kafka message, retrying. Attempt={Attempt}, MaxAttempts={MaxAttempts}, UpdateId={UpdateId}",
                    attempt,
                    attempts,
                    update.Id);

                if (backoff > TimeSpan.Zero)
                {
                    await Task.Delay(backoff, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to process Kafka message, no retries left. Attempts={Attempts}, UpdateId={UpdateId}",
                    attempts,
                    update.Id);

                return ex;
            }
        }

        return null;
    }

    private async Task<bool> TryPublishToDeadLetterAsync(
        ConsumeResult<string, byte[]> result,
        string reason,
        Exception? exception,
        CancellationToken ct)
    {
        try
        {
            await deadLetterPublisher.PublishAsync(result, reason, exception, ct);

            metrics.IncrementKafkaDeadLetter(result.Topic);

            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            metrics.IncrementKafkaDeadLetterError(result.Topic);

            logger.LogError(
                ex,
                "Failed to send Kafka message to DLQ. Offset will not be committed, the message will be replayed. Topic={Topic}, Partition={Partition}, Offset={Offset}, DeadLetterTopic={DeadLetterTopic}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                kafkaOptions.Value.DeadLetterTopic);

            return false;
        }
    }
}
