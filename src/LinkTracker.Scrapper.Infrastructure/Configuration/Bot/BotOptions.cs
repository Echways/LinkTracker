using LinkTracker.Shared.Infrastructure;

namespace LinkTracker.Scrapper.Infrastructure.Configuration.Bot;

public sealed class BotOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public TransportKind Transport { get; set; } = TransportKind.Kafka;
}