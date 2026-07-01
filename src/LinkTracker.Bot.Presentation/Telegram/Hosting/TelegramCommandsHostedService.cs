using LinkTracker.Bot.Application.Commands.Registration;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace LinkTracker.Bot.Presentation.Telegram.Hosting;

public sealed class TelegramCommandsHostedService(
    ITelegramBotClient botClient,
    CommandRegistry commandRegistry) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var commands = commandRegistry
            .GetTelegramCommands()
            .Select(x => new BotCommand { Command = x.Command, Description = x.Description });

        await botClient.SetMyCommands(commands, cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}