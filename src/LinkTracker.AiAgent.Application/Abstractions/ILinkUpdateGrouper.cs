using LinkTracker.Shared.Contracts.AiAgent;

namespace LinkTracker.AiAgent.Application.Abstractions;

public interface ILinkUpdateGrouper
{
    IReadOnlyList<ProcessedLinkUpdate> Group(IReadOnlyList<ProcessedLinkUpdate> updates);
}