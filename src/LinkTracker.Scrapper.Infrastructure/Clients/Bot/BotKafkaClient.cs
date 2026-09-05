using System.Diagnostics;
using System.Net;
using Confluent.Kafka;
using LinkTracker.Scrapper.Infrastructure.Configuration.Kafka;
using LinkTracker.Scrapper.Infrastructure.Kafka.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Telemetry;
using LinkTracker.Shared.Contracts.Bot;
using LinkTracker.Shared.Infrastructure;
using Microsoft.Extensions.Logging;

namespace LinkTracker.Scrapper.Infrastructure.Clients.Bot;

internal sealed class BotKafkaClient(
    IProducer<string, byte[]> producer,
    ILinkUpdateKafkaSerializer serializer,
    BotKafkaOptions options,
    ScrapperMetrics metrics,
    ILogger<BotKafkaClient> logger) : IBotTransportClient
{
    public TransportKind Transport => TransportKind.Kafka;

    public async Task SendUpdateAsync(LinkUpdate update, CancellationToken ct = default)
    {
        var payload = await serializer.SerializeAsync(update, options.Topic, ct);

        var message = new Message<string, byte[]> { Key = update.Id.ToString(), Value = payload };

        var sw = Stopwatch.StartNew();

        try
        {
            var result = await producer.ProduceAsync(options.Topic, message, ct);

            sw.Stop();

            metrics.KafkaProduced.Add(
                1,
                new KeyValuePair<string, object?>("topic", options.Topic));

            metrics.KafkaProduceDuration.Record(
                sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("topic", options.Topic));

            logger.LogInformation(
                "Kafka: Bot notification published. Topic={Topic}, Partition={Partition}, Offset={Offset}, UpdateId={UpdateId}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                update.Id);
        }
        catch (ProduceException<string, byte[]> ex)
        {
            sw.Stop();

            metrics.KafkaProduceErrors.Add(
                1,
                new KeyValuePair<string, object?>("topic", options.Topic));

            metrics.Errors.Add(
                1,
                new KeyValuePair<string, object?>("scope", "kafka_produce"),
                new KeyValuePair<string, object?>("scope_type", options.Topic),
                new KeyValuePair<string, object?>("reason", "produce_exception"));

            logger.LogWarning(
                ex,
                "Kafka: failed to publish Bot notification. Topic={Topic}, UpdateId={UpdateId}",
                options.Topic,
                update.Id);

            throw new BotClientException(
                HttpStatusCode.InternalServerError,
                $"Kafka produce failed: {ex.Error.Reason}");
        }
    }
}