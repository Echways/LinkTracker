using LinkTracker.Shared.Contracts.AiAgent;

namespace LinkTracker.AiAgent.Application.Abstractions;

public sealed record BufferedLinkUpdate(ProcessedLinkUpdate Update, IMessageAck Ack);

public sealed record GroupingBucket(long ChatId, IReadOnlyList<BufferedLinkUpdate> Updates);

public interface IGroupingBuffer
{
    void Add(long tgChatId, ProcessedLinkUpdate update, IMessageAck ack);

    IReadOnlyList<GroupingBucket> Flush(bool force = false);

    void Requeue(GroupingBucket bucket);
}