using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.AiAgent.Application.Abstractions;

public interface ILinkUpdateFilter
{
    bool ShouldFilter(LinkUpdate update);
}