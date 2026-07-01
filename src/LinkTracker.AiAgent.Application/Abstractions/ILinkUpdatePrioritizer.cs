using LinkTracker.Shared.Contracts.AiAgent;

namespace LinkTracker.AiAgent.Application.Abstractions;

public interface ILinkUpdatePrioritizer
{
    LinkUpdatePriority Prioritize(string description);
}