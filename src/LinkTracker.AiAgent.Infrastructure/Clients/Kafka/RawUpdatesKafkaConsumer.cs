using System.Diagnostics;
using Confluent.Kafka;
using LinkTracker.AiAgent.Application.Telemetry.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Configuration.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.AiAgent.Infrastructure.Clients.Kafka;

internal sealed class RawUpdatesKafkaConsumer(
    IConsumer<string, byte[]> consumer,
    IRawUpdatesKafkaMessageHandler messageHandler,
    IOptions<RawUpdatesKafkaOptions> kafkaOptions,
    IAiAgentMetrics metrics,
    ILogger<RawUpdatesKafkaConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return ConsumeLoopAsync(stoppingToken);
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
        finally
        {
            consumer.Close();
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