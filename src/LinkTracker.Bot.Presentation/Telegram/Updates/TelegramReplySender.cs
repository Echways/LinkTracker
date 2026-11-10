using LinkTracker.Bot.Application.Models;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace LinkTracker.Bot.Presentation.Telegram.Updates;

public sealed class TelegramReplySender(ITelegramBotClient botClient)
{
    public async Task SendAsync(OutgoingMessage reply, CancellationToken ct)
    {
        var markup = BuildMarkup(reply.Buttons);

        if (reply.EditMessageId is { } messageId)
        {
            await botClient.EditMessageText(
                reply.ChatId,
                messageId,
                reply.Text,
                replyMarkup: markup,
                cancellationToken: ct);
            return;
        }

        await botClient.SendMessage(
            reply.ChatId,
            reply.Text,
            replyMarkup: markup,
            cancellationToken: ct);
    }

    private static InlineKeyboardMarkup? BuildMarkup(IReadOnlyList<MessageButton>? buttons)
    {
        if (buttons is not { Count: > 0 })
        {
            return null;
        }

        return new InlineKeyboardMarkup(
            buttons.Select(b => InlineKeyboardButton.WithCallbackData(b.Text, b.CallbackData)));
    }
}
