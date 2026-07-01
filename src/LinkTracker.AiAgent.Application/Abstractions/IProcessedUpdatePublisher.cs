using LinkTracker.Shared.Contracts.AiAgent;

namespace LinkTracker.AiAgent.Application.Abstractions;

public interface IProcessedUpdatePublisher
{
    Task PublishAsync(ProcessedLinkUpdate update, CancellationToken ct);
}