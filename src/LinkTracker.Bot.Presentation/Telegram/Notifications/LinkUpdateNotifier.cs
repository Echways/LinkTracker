using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Bot.Application.Updates.Abstractions;
using LinkTracker.Shared.Contracts.AiAgent;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Localization;
using Telegram.Bot;

namespace LinkTracker.Bot.Presentation.Telegram.Notifications;

public sealed class LinkUpdateNotifier(
    ITelegramBotClient botClient,
    ChatLanguageService languages,
    IStringLocalizer<Messages> localizer,
    IBotMetrics metrics) : ILinkUpdateNotifier
{
    public async Task NotifyAsync(LinkUpdate update, CancellationToken ct = default)
    {
        foreach (var chatId in update.TgChatIds.Distinct())
        {
            var language = await languages.GetAsync(chatId, ct);
            using var _ = UiCulture.Use(language);

            var message = BuildMessage(update);

            await botClient.SendMessage(chatId, message, cancellationToken: ct);
            metrics.IncrementSentNotifications();
        }
    }

    private string BuildMessage(LinkUpdate update)
    {
        if (update.Kind == LinkUpdateKind.SystemReport)
        {
            return update.Description;
        }

        return $"{BuildHeader(update.Priority)}\n{update.Url}\n\n{update.Description}";
    }

    private string BuildHeader(LinkUpdatePriority priority)
    {
        return priority switch
        {
            LinkUpdatePriority.High => localizer[Texts.NotificationHigh],
            LinkUpdatePriority.Low => localizer[Texts.NotificationLow],
            _ => localizer[Texts.NotificationDefault]
        };
    }
}
