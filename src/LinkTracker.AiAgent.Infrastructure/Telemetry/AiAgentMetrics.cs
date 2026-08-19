using System.Diagnostics;
using System.Diagnostics.Metrics;
using LinkTracker.AiAgent.Application.Telemetry.Abstractions;

namespace LinkTracker.AiAgent.Infrastructure.Telemetry;

public sealed class AiAgentMetrics : IAiAgentMetrics, IDisposable
{
    public const string MeterName = "LinkTracker.AiAgent";

    private static readonly double[] DurationBuckets =
        [5, 10, 25, 50, 100, 250, 500, 1000, 2500];

    private readonly Counter<long> _kafkaConsumed;
    private readonly Histogram<double> _kafkaConsumeDuration;
    private readonly Counter<long> _kafkaConsumeErrors;
    private readonly Counter<long> _kafkaDeadLetterErrors;
    private readonly Counter<long> _kafkaDeadLetters;
    private readonly Counter<long> _summarizationFallbacks;
    private readonly Counter<long> _summarizations;

    private readonly Meter _meter;

    public AiAgentMetrics()
    {
        _meter = new Meter(MeterName);

        _kafkaConsumed = _meter.CreateCounter<long>(
            "kafka_consumed_total",
            description: "Количество обработанных сообщений из Kafka");

        _kafkaConsumeErrors = _meter.CreateCounter<long>(
            "kafka_consume_errors_total",
            description: "Количество ошибок обработки сообщений из Kafka");

        _kafkaDeadLetters = _meter.CreateCounter<long>(
            "kafka_dead_letter_total",
            description: "Количество сообщений, отправленных в DLQ");

        _kafkaDeadLetterErrors = _meter.CreateCounter<long>(
            "kafka_dead_letter_errors_total",
            description: "Количество неудачных отправок в DLQ (offset не подтверждается, сообщение переигрывается)");

        _kafkaConsumeDuration = _meter.CreateHistogram(
            "kafka_consume_duration_ms_total",
            null,
            "Длительность обработки сообщения из Kafka в миллисекундах",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationBuckets });

        _summarizations = _meter.CreateCounter<long>(
            "summarizations_total",
            description: "Количество успешных суммаризаций через Yandex AI");

        _summarizationFallbacks = _meter.CreateCounter<long>(
            "summarization_fallbacks_total",
            description: "Количество суммаризаций, деградировавших до обрезки текста, с разбивкой по причине");

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

    public void IncrementKafkaConsumed(string topic)
    {
        _kafkaConsumed.Add(
            1,
            new KeyValuePair<string, object?>("topic", topic));
    }

    public void IncrementKafkaConsumeError(string topic)
    {
        _kafkaConsumeErrors.Add(
            1,
            new KeyValuePair<string, object?>("topic", topic));
    }

    public void ObserveKafkaConsumeDuration(string topic, double milliseconds)
    {
        _kafkaConsumeDuration.Record(
            milliseconds,
            new KeyValuePair<string, object?>("topic", topic));
    }

    public void IncrementKafkaDeadLetter(string topic)
    {
        _kafkaDeadLetters.Add(
            1,
            new KeyValuePair<string, object?>("topic", topic));
    }

    public void IncrementKafkaDeadLetterError(string topic)
    {
        _kafkaDeadLetterErrors.Add(
            1,
            new KeyValuePair<string, object?>("topic", topic));
    }

    public void IncrementSummarization()
    {
        _summarizations.Add(1);
    }

    public void IncrementSummarizationFallback(string reason)
    {
        _summarizationFallbacks.Add(
            1,
            new KeyValuePair<string, object?>("reason", reason));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
