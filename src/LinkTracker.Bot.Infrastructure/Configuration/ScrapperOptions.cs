using LinkTracker.Shared.Infrastructure;

namespace LinkTracker.Bot.Infrastructure.Configuration;

public sealed class ScrapperOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public TransportKind Transport { get; set; } = TransportKind.Grpc;
}