using System.Collections.Concurrent;
using LinkTracker.Scrapper.Application.Abstractions.Updates;
using LinkTracker.Scrapper.Application.Clients.Bot;
using LinkTracker.Scrapper.Application.Models.Updates;
using LinkTracker.Scrapper.Application.Services.Updates;
using LinkTracker.Scrapper.Infrastructure.Outbox.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Outbox.Configuration;
using LinkTracker.Scrapper.Infrastructure.Quartz.Configuration;
using LinkTracker.Scrapper.Infrastructure.Telemetry;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace LinkTracker.Scrapper.Infrastructure.Quartz.Jobs;

[DisallowConcurrentExecution]
internal sealed class LinkUpdatesJob(
    ILinkTrackingStore trackingStore,
    IEnumerable<ILinkUpdateHandler> linkUpdateHandlers,
    IBotClient botClient,
    IOutboxStore outboxStore,
    IOptions<LinkUpdatesSchedulingOptions> schedulingOptions,
    IOptions<OutboxOptions> outboxOptions,
    TimeProvider timeProvider,
    ILogger<LinkUpdatesJob> logger,
    ScrapperMetrics metrics) : IJob
{
    private readonly int _batchSize = schedulingOptions.Value.BatchSize;
    private readonly int _maxDegreeOfParallelism = schedulingOptions.Value.MaxDegreeOfParallelism;
    private readonly bool _outboxEnabled = outboxOptions.Value.Enabled;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var runStartedAt = timeProvider.GetUtcNow();
        var linkCountBySource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        while (!ct.IsCancellationRequested)
        {
            var batch = await trackingStore.GetSubscriptionsDueForCheckAsync(runStartedAt, _batchSize, ct);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var subscription in batch)
            {
                var host = subscription.Url.Host;
                linkCountBySource[host] = linkCountBySource.GetValueOrDefault(host) + 1;
            }

            logger.LogDebug(
                "Started processing link batch. CheckedBefore={CheckedBefore}, BatchSize={BatchSize}, ActualCount={ActualCount}, MaxDegreeOfParallelism={MaxDegreeOfParallelism}",
                runStartedAt,
                _batchSize,
                batch.Count,
                _maxDegreeOfParallelism);

            var failedSubscriptions = new ConcurrentBag<TrackedLinkSubscription>();

            await Parallel.ForEachAsync(
                batch,
                new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism, CancellationToken = ct },
                async (subscription, token) =>
                {
                    try
                    {
                        await ProcessSubscriptionAsync(subscription, token);
                    }
                    catch (Exception ex)
                    {
                        failedSubscriptions.Add(subscription);

                        logger.LogError(
                            ex,
                            "Failed to process link. LinkId={LinkId}, Url={Url}",
                            subscription.Id,
                            subscription.Url);
                    }
                });

            await trackingStore.MarkCheckedAsync(
                batch.Select(x => x.Id).ToArray(),
                timeProvider.GetUtcNow(),
                ct);

            if (!failedSubscriptions.IsEmpty)
            {
                var failed = failedSubscriptions
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .OrderBy(x => x.Id)
                    .ToArray();

                logger.LogWarning(
                    "Batch processed with errors. FailedCount={FailedCount}, FailedLinkIds={FailedLinkIds}, FailedUrls={FailedUrls}",
                    failed.Length,
                    failed.Select(x => x.Id).ToArray(),
                    failed.Select(x => x.Url.ToString()).ToArray());

                await SendFailedReportsAsync(failed, ct);
            }
        }

        foreach (var (source, count) in linkCountBySource)
        {
            metrics.SetLinksOnTrack(source, count);
        }
    }

    private async Task ProcessSubscriptionAsync(
        TrackedLinkSubscription subscription,
        CancellationToken ct)
    {
        var handler = linkUpdateHandlers.FirstOrDefault(x => x.CanHandle(subscription.Url));

        if (handler is null)
        {
            logger.LogDebug(
                "No handler found for link. LinkId={LinkId}, Url={Url}",
                subscription.Id,
                subscription.Url);

            return;
        }

        var checkResult = await handler.CheckAsync(subscription, ct);

        if (!checkResult.HasChanges)
        {
            await UpdateCursorAsync(checkResult, subscription, ct);
            return;
        }

        var updates = checkResult.Events
            .Select(linkEvent => LinkUpdatePayloadMapper.ToBotUpdate(subscription, linkEvent))
            .ToArray();

        if (_outboxEnabled)
        {
            await SaveUpdatesToOutboxAsync(subscription, checkResult, updates, ct);
            return;
        }

        foreach (var update in updates)
        {
            await botClient.SendUpdateAsync(update, ct);

            logger.LogDebug(
                "Update sent. LinkId={LinkId}, Url={Url}, ChatCount={ChatCount}",
                subscription.Id,
                subscription.Url,
                subscription.TgChatIds.Count);
        }

        metrics.SentUpdates.Add(updates.Length);

        await UpdateCursorAsync(checkResult, subscription, ct);
    }

    private async Task UpdateCursorAsync(
        LinkCheckResult checkResult,
        TrackedLinkSubscription subscription,
        CancellationToken ct)
    {
        if (checkResult.NewLastUpdatedAt is null)
        {
            return;
        }

        if (checkResult.NewLastUpdatedAt == subscription.LastUpdatedAt &&
            checkResult.NewLastEventKey == subscription.LastEventKey)
        {
            return;
        }

        await trackingStore.SetCursorAsync(
            subscription.Id,
            checkResult.NewLastUpdatedAt.Value,
            checkResult.NewLastEventKey,
            ct);
    }

    private async Task SendFailedReportsAsync(
        IReadOnlyCollection<TrackedLinkSubscription> failedSubscriptions,
        CancellationToken ct)
    {
        var reportsByChat = failedSubscriptions
            .SelectMany(subscription => subscription.TgChatIds.Distinct()
                .Select(chatId => new { ChatId = chatId, subscription.Url }))
            .GroupBy(x => x.ChatId, x => x.Url)
            .Select(group => new
            {
                ChatId = group.Key,
                Urls = group
                    .Distinct()
                    .OrderBy(x => x.ToString())
                    .ToArray()
            });

        foreach (var report in reportsByChat)
        {
            try
            {
                await botClient.SendUpdateAsync(
                    new LinkUpdate
                    {
                        Id = 0,
                        Url = report.Urls[0],
                        TgChatIds = [report.ChatId],
                        Description = BuildFailedReportDescription(report.Urls),
                        Kind = LinkUpdateKind.SystemReport
                    },
                    ct);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to send the failed-links report. ChatId={ChatId}, UrlCount={UrlCount}",
                    report.ChatId,
                    report.Urls.Length);
            }
        }
    }

    private static string BuildFailedReportDescription(IReadOnlyCollection<Uri> urls)
    {
        var newLine = Environment.NewLine;
        var urlLines = string.Join(newLine, urls.Select(url => $"- {url}"));

        return
            $"Не удалось проверить часть ссылок в текущем цикле:{newLine}" +
            $"{urlLines}{newLine}{newLine}" +
            "Остальные ссылки были обработаны. Повторим попытку в следующем запуске.";
    }

    private async Task SaveUpdatesToOutboxAsync(
        TrackedLinkSubscription subscription,
        LinkCheckResult checkResult,
        IReadOnlyCollection<LinkUpdate> updates,
        CancellationToken ct)
    {
        if (updates.Count == 0)
        {
            await UpdateCursorAsync(checkResult, subscription, ct);
            return;
        }

        if (checkResult.NewLastUpdatedAt is null)
        {
            throw new InvalidOperationException(
                $"Cannot save a link update to the outbox without a cursor. LinkId={subscription.Id}");
        }

        await outboxStore.AddRangeAndSetCursorAsync(
            subscription.Id,
            checkResult.NewLastUpdatedAt,
            checkResult.NewLastEventKey,
            updates,
            ct);

        metrics.OutboxEnqueuedUpdates.Add(updates.Count);

        logger.LogDebug(
            "Updates saved to the transactional outbox. LinkId={LinkId}, Url={Url}, UpdateCount={UpdateCount}, ChatCount={ChatCount}",
            subscription.Id,
            subscription.Url,
            updates.Count,
            subscription.TgChatIds.Count);
    }
}