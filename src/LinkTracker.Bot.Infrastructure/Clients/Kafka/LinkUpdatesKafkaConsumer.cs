using System.Diagnostics;
using Confluent.Kafka;
using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Bot.Infrastructure.Abstractions.Kafka;
using LinkTracker.Bot.Infrastructure.Configuration.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.Bot.Infrastructure.Clients.Kafka;

internal sealed class LinkUpdatesKafkaConsumer(
    IConsumer<string, byte[]> consumer,
    ILinkUpdatesKafkaMessageHandler messageHandler,
    IOptions<LinkUpdatesKafkaOptions> kafkaOptions,
    IBotMetrics metrics,
    ILogger<LinkUpdatesKafkaConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return ConsumeLoopAsync(stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        consumer.Close();
        return base.StopAsync(cancellationToken);
    }

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        var topic = kafkaOptions.Value.Topic;
        consumer.Subscribe(topic);

        logger.LogInformation(
            "Kafka consumer запущен. Topic={Topic}, GroupId={GroupId}",
            topic,
            kafkaOptions.Value.GroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, byte[]>? result;

                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    logger.LogWarning(ex, "Kafka consume завершился ошибкой.");
                    continue;
                }

                if (result is null)
                {
                    continue;
                }

                await ProcessMessageAsync(result, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Kafka consumer остановлен.");
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, byte[]> result, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var shouldCommit = await messageHandler.HandleAsync(result, ct);

            sw.Stop();

            metrics.IncrementKafkaConsumed(result.Topic);
            metrics.ObserveKafkaConsumeDuration(result.Topic, sw.Elapsed.TotalMilliseconds);

            if (shouldCommit)
            {
                TryCommit(result);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();

            metrics.IncrementKafkaConsumeError(result.Topic);
            metrics.ObserveKafkaConsumeDuration(result.Topic, sw.Elapsed.TotalMilliseconds);
            metrics.IncrementError("kafka_consume", result.Topic, "handler_exception");

            logger.LogError(
                ex,
                "Ошибка обработки Kafka сообщения. Offset не будет подтвержден. Topic={Topic}, Partition={Partition}, Offset={Offset}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value);
        }
    }

    private bool TryCommit(ConsumeResult<string, byte[]> result)
    {
        try
        {
            consumer.Commit(result);
            return true;
        }
        catch (KafkaException ex)
        {
            logger.LogError(
                ex,
                "Не удалось подтвердить Kafka offset. Topic={Topic}, Partition={Partition}, Offset={Offset}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value);

            return false;
        }
    }
}