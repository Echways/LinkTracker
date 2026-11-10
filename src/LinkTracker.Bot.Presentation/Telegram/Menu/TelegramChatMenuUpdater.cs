using LinkTracker.Bot.Application.Commands.Registration;
using LinkTracker.Bot.Application.Localization;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace LinkTracker.Bot.Presentation.Telegram.Menu;

public sealed class TelegramChatMenuUpdater(
    ITelegramBotClient botClient,
    CommandRegistry commandRegistry,
    ILogger<TelegramChatMenuUpdater> logger) : IChatMenuUpdater
{
    public async Task UpdateAsync(long chatId, BotLanguage language, CancellationToken ct)
    {
        try
        {
            await botClient.SetMyCommands(
                BuildCommands(language),
                new BotCommandScopeChat { ChatId = chatId },
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to update the command menu. ChatId={ChatId}, Language={Language}", chatId, language.Code);
        }
    }

    internal IReadOnlyList<BotCommand> BuildCommands(BotLanguage language)
    {
        using var _ = UiCulture.Use(language);

        return commandRegistry
            .GetTelegramCommands()
            .Select(x => new BotCommand { Command = x.Command, Description = x.Description })
            .ToList();
    }
}
