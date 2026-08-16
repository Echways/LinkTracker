using System.Diagnostics;
using System.Diagnostics.Metrics;
using LinkTracker.Bot.Application.Telemetry.Abstractions;

namespace LinkTracker.Bot.Infrastructure.Telemetry;

public sealed class BotMetrics : IBotMetrics, IDisposable
{
    public const string MeterName = "LinkTracker.Bot";

    private static readonly double[] DurationBuckets =
        [5, 10, 25, 50, 100, 250, 500, 1000, 2500];

    private readonly Histogram<double> _commandDuration;

    private readonly Counter<long> _commandRequests;
    private readonly Counter<long> _errors;
    private readonly Counter<long> _kafkaConsumed;
    private readonly Histogram<double> _kafkaConsumeDuration;
    private readonly Counter<long> _kafkaConsumeErrors;
    private readonly Counter<long> _kafkaDeadLetterErrors;
    private readonly Counter<long> _kafkaDeadLetters;

    private readonly Meter _meter;
    private readonly Histogram<double> _scrapperCallDuration;
    private readonly Counter<long> _sentNotifications;
    private readonly Counter<long> _totalRequests;

    public BotMetrics()
    {
        _meter = new Meter(MeterName);

        _commandRequests = _meter.CreateCounter<long>(
            "command_requests_total",
            description: "Количество вызовов команд");

        _commandDuration = _meter.CreateHistogram(
            "command_duration_ms_total",
            null,
            "Длительность выполнения команды в миллисекундах",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationBuckets });

        _scrapperCallDuration = _meter.CreateHistogram(
            "scrapper_call_duration_ms_total",
            null,
            "Длительность вызовов к Scrapper в миллисекундах",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationBuckets });

        _sentNotifications = _meter.CreateCounter<long>(
            "sent_notification_total",
            description: "Количество отправленных уведомлений");

        _totalRequests = _meter.CreateCounter<long>(
            "bot_requests_total",
            description: "Количество входящих запросов к боту");

        _errors = _meter.CreateCounter<long>(
            "errors_total",
            description: "Количество ошибок (RED: Errors) с разбивкой по scope и типу");

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

    public void IncrementCommand(string command)
    {
        _commandRequests.Add(
            1,
            new KeyValuePair<string, object?>("command", command));
    }

    public void ObserveCommandDuration(string scope, string scopeType, double milliseconds)
    {
        _commandDuration.Record(
            milliseconds,
            new KeyValuePair<string, object?>("scope", scope),
            new KeyValuePair<string, object?>("scope_type", scopeType));
    }

    public void ObserveScrapperCallDuration(string scope, string scopeType, double milliseconds)
    {
        _scrapperCallDuration.Record(
            milliseconds,
            new KeyValuePair<string, object?>("scope", scope),
            new KeyValuePair<string, object?>("scope_type", scopeType));
    }

    public void IncrementSentNotifications()
    {
        _sentNotifications.Add(1);
    }

    public void IncrementRequest(string requestType)
    {
        _totalRequests.Add(
            1,
            new KeyValuePair<string, object?>("request_type", requestType));
    }

    public void IncrementError(string scope, string scopeType, string reason)
    {
        _errors.Add(
            1,
            new KeyValuePair<string, object?>("scope", scope),
            new KeyValuePair<string, object?>("scope_type", scopeType),
            new KeyValuePair<string, object?>("reason", reason));
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

    public void Dispose()
    {
        _meter.Dispose();
    }
}