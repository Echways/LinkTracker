using LinkTracker.Shared.Contracts.AiAgent;

namespace LinkTracker.AiAgent.Application.Abstractions;

public interface IGroupingBuffer
{
    void Add(long tgChatId, ProcessedLinkUpdate update);
    IReadOnlyList<(long ChatId, IReadOnlyList<ProcessedLinkUpdate> Updates)> Flush();
}