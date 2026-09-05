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
    KafkaOffsetTracker offsetTracker,
    IOptions<RawUpdatesKafkaOptions> kafkaOptions,
    IAiAgentMetrics metrics,
    ILogger<RawUpdatesKafkaConsumer> logger) : BackgroundService
{
    private static readonly TimeSpan PollTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ConsumeErrorBackoff = TimeSpan.FromSeconds(1);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Factory.StartNew(
                () => ConsumeLoopAsync(stoppingToken),
                stoppingToken,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default)
            .Unwrap();
    }

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        var topic = kafkaOptions.Value.Topic;
        consumer.Subscribe(topic);

        logger.LogInformation(
            "Kafka consumer started. Topic={Topic}, GroupId={GroupId}",
            topic,
            kafkaOptions.Value.GroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, byte[]>? result;

                try
                {
                    result = consumer.Consume(PollTimeout);
                }
                catch (ConsumeException ex)
                {
                    logger.LogWarning(ex, "Kafka consume failed, backing off before retry.");
                    await Task.Delay(ConsumeErrorBackoff, stoppingToken);
                    continue;
                }

                if (result is not null && !result.IsPartitionEOF)
                {
                    await ProcessMessageAsync(result, stoppingToken);
                }

                CommitCompleted();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Kafka consumer stopped.");
        }
        finally
        {
            CommitCompleted();
            consumer.Close();
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, byte[]> result, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        var ack = offsetTracker.Track(result);

        try
        {
            var handled = await messageHandler.HandleAsync(result, ack, ct);

            sw.Stop();

            metrics.IncrementKafkaConsumed(result.Topic);
            metrics.ObserveKafkaConsumeDuration(result.Topic, sw.Elapsed.TotalMilliseconds);

            if (handled)
            {
                ack.Release();
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
                "Failed to process Kafka message. Offset will not be committed. Topic={Topic}, Partition={Partition}, Offset={Offset}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value);
        }
    }

    private void CommitCompleted()
    {
        var offsets = offsetTracker.TakeCommittableOffsets();

        if (offsets.Count == 0)
        {
            return;
        }

        try
        {
            consumer.Commit(offsets);
        }
        catch (KafkaException ex)
        {
            logger.LogError(
                ex,
                "Failed to commit Kafka offsets. Offsets={Offsets}",
                string.Join(", ", offsets));
        }
    }
}
