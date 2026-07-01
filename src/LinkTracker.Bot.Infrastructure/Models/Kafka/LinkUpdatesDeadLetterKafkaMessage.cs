namespace LinkTracker.Bot.Infrastructure.Models.Kafka;

internal sealed class LinkUpdatesDeadLetterKafkaMessage
{
    public string Payload { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string? ExceptionType { get; init; }

    public string SourceTopic { get; init; } = string.Empty;

    public int SourcePartition { get; init; }

    public long SourceOffset { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}