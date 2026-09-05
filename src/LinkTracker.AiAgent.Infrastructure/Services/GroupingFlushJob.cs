using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.Shared.Contracts.AiAgent;
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
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await FlushAsync(false, stoppingToken);
        }
    }

   public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await FlushAsync(true, cancellationToken);
    }

    private async Task FlushAsync(bool force, CancellationToken ct)
    {
        var pending = buffer.Flush(force)
            .Select(bucket => new
            {
                Bucket = bucket,
                Groups = grouper.Group(bucket.Updates.Select(x => x.Update).ToArray())
            })
            .OrderByDescending(x => x.Groups.Max(group => group.Priority))
            .ToArray();

        foreach (var entry in pending)
        {
            if (!await TryPublishAsync(entry.Bucket, entry.Groups, ct))
            {
                buffer.Requeue(entry.Bucket);

                if (ct.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task<bool> TryPublishAsync(
        GroupingBucket bucket,
        IReadOnlyList<ProcessedLinkUpdate> groups,
        CancellationToken ct)
    {
        try
        {
            foreach (var group in groups)
            {
                await publisher.PublishAsync(group, ct);

                logger.LogInformation(
                    "Group published. ChatId={ChatId}, UpdateId={UpdateId}, Priority={Priority}",
                    bucket.ChatId, group.Id, group.Priority);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish the grouped update, the window was returned to the buffer. ChatId={ChatId}",
                bucket.ChatId);

            return false;
        }

        foreach (var buffered in bucket.Updates)
        {
            buffered.Ack.Release();
        }

        return true;
    }
}
