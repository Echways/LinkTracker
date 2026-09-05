using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Routing;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace LinkTracker.Bot.Presentation.Telegram.Updates;

public sealed class UpdateMapper(UpdateRouter router, ILogger<UpdateMapper> logger)
{
    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        var request = Map(update);
        if (request is null)
        {
            return;
        }

        var reply = await router.RouteAsync(request, ct);

        await botClient.SendMessage(
            reply.ChatId,
            reply.Text,
            cancellationToken: ct
        );
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Polling failed.");
        return Task.CompletedTask;
    }

    private static BotRequest? Map(Update update)
    {
        if (update is { Type: UpdateType.Message, Message: { } msg })
        {
            var chatId = msg.Chat.Id;

            if (msg.Contact is { } contact)
            {
                return new BotRequest(
                    chatId,
                    BotRequestType.Contact,
                    Phone: contact.PhoneNumber
                );
            }

            if (string.IsNullOrWhiteSpace(msg.Text))
            {
                return null;
            }

            var text = msg.Text.Trim();

            if (!text.StartsWith('/'))
            {
                return new BotRequest(
                    chatId,
                    BotRequestType.Text,
                    text
                );
            }

            var token = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
            var cmd = token.TrimStart('/').Split('@', 2)[0];

            return new BotRequest(
                chatId,
                BotRequestType.Command,
                text,
                cmd
            );
        }

        if (update.Type != UpdateType.CallbackQuery || update.CallbackQuery is not { } cb)
        {
            return null;
        }

        {
            var chatId = cb.Message?.Chat.Id;
            if (chatId is null)
            {
                return null;
            }

            return new BotRequest(
                chatId.Value,
                BotRequestType.Callback,
                CallbackData: cb.Data
            );
        }
    }
}