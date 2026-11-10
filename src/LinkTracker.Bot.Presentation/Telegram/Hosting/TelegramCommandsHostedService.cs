using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Presentation.Telegram.Menu;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

namespace LinkTracker.Bot.Presentation.Telegram.Hosting;

public sealed class TelegramCommandsHostedService(
    ITelegramBotClient botClient,
    TelegramChatMenuUpdater menuUpdater) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await botClient.SetMyCommands(
            menuUpdater.BuildCommands(BotLanguage.Default),
            cancellationToken: cancellationToken);

        foreach (var language in BotLanguage.All)
        {
            await botClient.SetMyCommands(
                menuUpdater.BuildCommands(language),
                languageCode: language.Code,
                cancellationToken: cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
