namespace LinkTracker.Bot.Application.Telemetry.Abstractions;

public interface IBotMetrics
{
    void IncrementCommand(string command);

    void ObserveCommandDuration(string scope, string scopeType, double milliseconds);

    void ObserveScrapperCallDuration(string scope, string scopeType, double milliseconds);

    void IncrementSentNotifications();

    void IncrementRequest(string requestType);

    void IncrementError(string scope, string scopeType, string reason);

    void IncrementKafkaConsumed(string topic);

    void IncrementKafkaConsumeError(string topic);

    void ObserveKafkaConsumeDuration(string topic, double milliseconds);
}