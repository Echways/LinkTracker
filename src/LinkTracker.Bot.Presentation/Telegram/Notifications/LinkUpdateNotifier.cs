using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Bot.Application.Updates.Abstractions;
using LinkTracker.Shared.Constants;
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
        if (update.Description.StartsWith(SystemMessageMarkers.FailedLinkReport, StringComparison.Ordinal))
        {
            return update.Description[SystemMessageMarkers.FailedLinkReport.Length..];
        }

        return $"Обновление по ссылке:\n{update.Url}\n\n{update.Description}";
    }
}