using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.AiAgent.Infrastructure.Services;

internal sealed class GroupingFlushJob(
    IGroupingBuffer buffer,
    ILinkUpdateGrouper grouper,
    IProcessedUpdatePublisher publisher,
    IOptions<AiAgentOptions> options,
    ILogger<GroupingFlushJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(options.Value.Grouping.FlushIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);
            await FlushAsync(stoppingToken);
        }
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        var pending = buffer.Flush()
            .SelectMany(entry => grouper
                .Group(entry.Updates)
                .Select(update => (entry.ChatId, Update: update)))
            .OrderByDescending(x => x.Update.Priority)
            .ToArray();

        foreach (var (chatId, update) in pending)
        {
            try
            {
                await publisher.PublishAsync(update, ct);

                logger.LogInformation(
                    "Группа опубликована. ChatId={ChatId}, UpdateId={UpdateId}, Priority={Priority}",
                    chatId, update.Id, update.Priority);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Ошибка публикации сгруппированного обновления. ChatId={ChatId}", chatId);
            }
        }
    }
}