namespace LinkTracker.AiAgent.Application.Telemetry.Abstractions;

public interface IAiAgentMetrics
{
    void IncrementKafkaConsumed(string topic);

    void IncrementKafkaConsumeError(string topic);

    void ObserveKafkaConsumeDuration(string topic, double milliseconds);

    void IncrementKafkaDeadLetter(string topic);

    void IncrementKafkaDeadLetterError(string topic);

    void IncrementSummarization();

    void IncrementSummarizationFallback(string reason);
}
