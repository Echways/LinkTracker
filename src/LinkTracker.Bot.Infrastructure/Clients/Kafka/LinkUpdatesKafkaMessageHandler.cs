using Confluent.Kafka;
using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Bot.Application.Updates.Abstractions;
using LinkTracker.Bot.Infrastructure.Abstractions.Kafka;
using LinkTracker.Bot.Infrastructure.Configuration.Kafka;
using LinkTracker.Bot.Infrastructure.Kafka.Abstractions;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.Bot.Infrastructure.Clients.Kafka;

internal sealed class LinkUpdatesKafkaMessageHandler(
    ILinkUpdateKafkaDeserializer deserializer,
    KafkaLinkUpdateMessageParser parser,
    ILinkUpdateDeadLetterPublisher deadLetterPublisher,
    ILinkUpdateNotifier notifier,
    IOptions<LinkUpdatesKafkaOptions> kafkaOptions,
    IBotMetrics metrics,
    ILogger<LinkUpdatesKafkaMessageHandler> logger) : ILinkUpdatesKafkaMessageHandler
{
    public async Task<bool> HandleAsync(ConsumeResult<string, byte[]> result, CancellationToken ct)
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
                $"Kafka сообщение не удалось десериализовать: {ex.Message}",
                ex,
                ct);
        }

        if (!parser.TryValidate(update, out var error))
        {
            return await TryPublishToDeadLetterAsync(
                result,
                error ?? "Kafka сообщение не прошло валидацию.",
                null,
                ct);
        }

        var notificationError = await TryNotifyWithRetriesAsync(update!, ct);

        if (notificationError is not null)
        {
            return await TryPublishToDeadLetterAsync(
                result,
                "Исчерпаны попытки обработки Kafka сообщения.",
                notificationError,
                ct);
        }

        logger.LogInformation(
            "Kafka сообщение обработано. Topic={Topic}, Partition={Partition}, Offset={Offset}, UpdateId={UpdateId}",
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            update!.Id);

        return true;
    }

    private async Task<Exception?> TryNotifyWithRetriesAsync(LinkUpdate update, CancellationToken ct)
    {
        var attempts = Math.Max(1, kafkaOptions.Value.RetryAttempts);
        var backoff = TimeSpan.FromMilliseconds(Math.Max(0, kafkaOptions.Value.RetryBackoffMilliseconds));

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await notifier.NotifyAsync(update, ct);
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
                    "Ошибка обработки Kafka сообщения. Будет повторная попытка. Attempt={Attempt}, MaxAttempts={MaxAttempts}, UpdateId={UpdateId}",
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
                    "Ошибка обработки Kafka сообщения. Повторные попытки закончились. Attempts={Attempts}, UpdateId={UpdateId}",
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
            metrics.IncrementError("kafka_dead_letter", result.Topic, "publish_failed");

            logger.LogError(
                ex,
                "Не удалось отправить Kafka сообщение в DLQ. Offset не будет подтвержден, сообщение будет переигрываться. Topic={Topic}, Partition={Partition}, Offset={Offset}, DeadLetterTopic={DeadLetterTopic}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                kafkaOptions.Value.DeadLetterTopic);

            return false;
        }
    }
}