using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LinkTracker.Scrapper.Infrastructure.Telemetry;

public sealed class ScrapperMetrics : IDisposable
{
    public const string MeterName = "LinkTracker.Scrapper";

    private static readonly double[] HttpBuckets =
        [5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000];

    private static readonly double[] DbKafkaBuckets =
        [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500];

    private readonly ConcurrentDictionary<string, long> _linksOnTrack = new();

    private readonly Meter _meter;

    public ScrapperMetrics()
    {
        _meter = new Meter(MeterName);

        _meter.CreateObservableGauge(
            "links_on_track_total",
            ObserveLinksOnTrack,
            description: "Количество ссылок в БД, поставленных на мониторинг");

        ApiRequests = _meter.CreateCounter<long>(
            "api_requests_total",
            description: "Счётчик пришедших к API запросов");

        SentUpdates = _meter.CreateCounter<long>(
            "sent_updates_total",
            description: "Количество обновлений, фактически отправленных в Bot Service");

        OutboxEnqueuedUpdates = _meter.CreateCounter<long>(
            "outbox_enqueued_updates_total",
            description: "Количество обновлений, записанных в transactional outbox");

        RequestDuration = _meter.CreateHistogram(
            "request_duration_ms_total",
            null,
            "Длительность одной операции в миллисекундах",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = HttpBuckets });

        Errors = _meter.CreateCounter<long>(
            "errors_total",
            description: "Количество ошибок (RED: Errors) с разбивкой по scope и типу");

        DbQueries = _meter.CreateCounter<long>(
            "db_queries_total",
            description: "Количество запросов к БД с разбивкой по операции");

        DbErrors = _meter.CreateCounter<long>(
            "db_errors_total",
            description: "Количество ошибок при работе с БД");

        DbQueryDuration = _meter.CreateHistogram(
            "db_query_duration_ms_total",
            null,
            "Длительность запроса к БД в миллисекундах",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DbKafkaBuckets });

        KafkaProduced = _meter.CreateCounter<long>(
            "kafka_produced_total",
            description: "Количество сообщений, опубликованных в Kafka");

        KafkaProduceErrors = _meter.CreateCounter<long>(
            "kafka_produce_errors_total",
            description: "Количество ошибок публикации в Kafka");

        KafkaProduceDuration = _meter.CreateHistogram(
            "kafka_produce_duration_ms_total",
            null,
            "Длительность публикации сообщения в Kafka в миллисекундах",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DbKafkaBuckets });

        _meter.CreateObservableGauge(
            "process_memory_working_set_bytes",
            static () => Process.GetCurrentProcess().WorkingSet64,
            null,
            "Резидентная память процесса (working set) в байтах");

        _meter.CreateObservableGauge(
            "process_memory_managed_bytes",
            static () => GC.GetTotalMemory(false),
            null,
            "Управляемая память (GC) в байтах");
    }

    public Counter<long> ApiRequests { get; }

    public Counter<long> SentUpdates { get; }

    public Counter<long> OutboxEnqueuedUpdates { get; }

    public Histogram<double> RequestDuration { get; }

    public Counter<long> Errors { get; }

    public Counter<long> DbQueries { get; }

    public Counter<long> DbErrors { get; }

    public Histogram<double> DbQueryDuration { get; }

    public Counter<long> KafkaProduced { get; }

    public Counter<long> KafkaProduceErrors { get; }

    public Histogram<double> KafkaProduceDuration { get; }

    public void Dispose()
    {
        _meter.Dispose();
    }

    public void SetLinksOnTrack(string trackedSource, long count)
    {
        _linksOnTrack[trackedSource] = count;
    }

    private IEnumerable<Measurement<long>> ObserveLinksOnTrack()
    {
        return _linksOnTrack.Select(kv => new Measurement<long>(
            kv.Value,
            new KeyValuePair<string, object?>("tracked_source", kv.Key)));
    }
}