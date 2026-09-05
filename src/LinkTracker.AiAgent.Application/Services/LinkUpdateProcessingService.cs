using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.Shared.Contracts.AiAgent;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Logging;

namespace LinkTracker.AiAgent.Application.Services;

public sealed class LinkUpdateProcessingService(
    ILinkUpdateFilter filter,
    ILinkUpdateSummarizer summarizer,
    ILinkUpdatePrioritizer prioritizer,
    IGroupingBuffer groupingBuffer,
    IProcessedUpdatePublisher publisher,
    ILogger<LinkUpdateProcessingService> logger) : ILinkUpdateProcessingService
{
    public async Task ProcessAsync(LinkUpdate update, IMessageAck ack, CancellationToken ct = default)
    {
        if (update.Kind == LinkUpdateKind.SystemReport)
        {
            await PublishSystemReportAsync(update, ct);
            return;
        }

        if (filter.ShouldFilter(update))
        {
            logger.LogDebug(
                "Update filtered out. UpdateId={UpdateId}, Author={Author}",
                update.Id, update.Author);
            return;
        }

        var description = await summarizer.SummarizeAsync(update.Description, ct);
        var priority = prioritizer.Prioritize(description);

        foreach (var chatId in update.TgChatIds)
        {
            groupingBuffer.Add(
                chatId,
                new ProcessedLinkUpdate
                {
                    Id = update.Id,
                    Url = update.Url,
                    Description = description,
                    TgChatIds = [chatId],
                    Priority = priority
                },
                ack);
        }

        logger.LogDebug(
            "Update added to the buffer. UpdateId={UpdateId}, Priority={Priority}",
            update.Id, priority);
    }

    private async Task PublishSystemReportAsync(LinkUpdate update, CancellationToken ct)
    {
        foreach (var chatId in update.TgChatIds)
        {
            await publisher.PublishAsync(
                new ProcessedLinkUpdate
                {
                    Id = update.Id,
                    Url = update.Url,
                    Description = update.Description,
                    TgChatIds = [chatId],
                    Priority = update.Priority,
                    Kind = LinkUpdateKind.SystemReport
                },
                ct);
        }

        logger.LogDebug("System report published without processing. UpdateId={UpdateId}", update.Id);
    }
}
