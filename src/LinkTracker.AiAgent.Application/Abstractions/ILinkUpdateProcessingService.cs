using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.AiAgent.Application.Abstractions;

public interface ILinkUpdateProcessingService
{
    Task ProcessAsync(LinkUpdate update, CancellationToken ct = default);
}