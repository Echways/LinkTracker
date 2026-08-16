using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Bot.Application.Updates.Abstractions;
using LinkTracker.Shared.Contracts.AiAgent;
using LinkTracker.Shared.Contracts.Bot;
using Telegram.Bot;

namespace LinkTracker.Bot.Presentation.Telegram.Notifications;

public sealed class LinkUpdateNotifier(ITelegramBotClient botClient, IBotMetrics metrics) : ILinkUpdateNotifier
{
    public async Task NotifyAsync(LinkUpdate update, CancellationToken ct = default)
    {
        var message = BuildMessage(update);

        foreach (var chatId in update.TgChatIds.Distinct())
        {
            await botClient.SendMessage(chatId, message, cancellationToken: ct);
            metrics.IncrementSentNotifications();
        }
    }

    private static string BuildMessage(LinkUpdate update)
    {
        if (update.Kind == LinkUpdateKind.SystemReport)
        {
            return update.Description;
        }

        return $"{BuildHeader(update.Priority)}\n{update.Url}\n\n{update.Description}";
    }

    private static string BuildHeader(LinkUpdatePriority priority)
    {
        return priority switch
        {
            LinkUpdatePriority.High => "‼️ Важное обновление по ссылке:",
            LinkUpdatePriority.Low => "Незначительное обновление по ссылке:",
            _ => "Обновление по ссылке:"
        };
    }
}
