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
    ILogger<LinkUpdateProcessingService> logger) : ILinkUpdateProcessingService
{
    public async Task ProcessAsync(LinkUpdate update, CancellationToken ct)
    {
        if (filter.ShouldFilter(update))
        {
            logger.LogDebug(
                "Обновление отфильтровано. UpdateId={UpdateId}, Author={Author}",
                update.Id, update.Author);
            return;
        }

        var description = await summarizer.SummarizeAsync(update.Description, ct);
        var priority = prioritizer.Prioritize(description);

        foreach (var chatId in update.TgChatIds)
        {
            groupingBuffer.Add(chatId, new ProcessedLinkUpdate
            {
                Id = update.Id,
                Url = update.Url,
                Description = description,
                TgChatIds = [chatId],
                Priority = priority
            });
        }

        logger.LogDebug(
            "Обновление добавлено в буфер. UpdateId={UpdateId}, Priority={Priority}",
            update.Id, priority);
    }
}