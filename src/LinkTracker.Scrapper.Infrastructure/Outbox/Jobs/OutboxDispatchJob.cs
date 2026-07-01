using LinkTracker.Scrapper.Infrastructure.Clients.Bot;
using LinkTracker.Scrapper.Infrastructure.Outbox.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Outbox.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace LinkTracker.Scrapper.Infrastructure.Outbox.Jobs;

[DisallowConcurrentExecution]
internal sealed class OutboxDispatchJob(
    IOutboxStore outboxStore,
    IBotDirectClient botClient,
    IOptions<OutboxOptions> outboxOptions,
    ILogger<OutboxDispatchJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var options = outboxOptions.Value;

        var messages = await outboxStore.GetUnprocessedBatchAsync(
            options.BatchSize,
            options.MaxRetryCount,
            ct);

        if (messages.Count == 0)
        {
            return;
        }

        logger.LogInformation("Начата отправка outbox сообщений. Count={Count}", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                await botClient.SendUpdateAsync(message.Payload, ct);
                await outboxStore.MarkProcessedAsync(message.Id, ct);

                logger.LogDebug(
                    "Outbox сообщение отправлено. OutboxMessageId={OutboxMessageId}",
                    message.Id);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await outboxStore.MarkFailedAsync(message.Id, ex.Message, ct);

                logger.LogWarning(
                    ex,
                    "Не удалось отправить outbox сообщение. OutboxMessageId={OutboxMessageId}, RetryCount={RetryCount}",
                    message.Id,
                    message.RetryCount + 1);
            }
        }
    }
}