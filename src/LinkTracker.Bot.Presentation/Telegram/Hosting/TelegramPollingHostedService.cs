using LinkTracker.Bot.Presentation.Telegram.Updates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace LinkTracker.Bot.Presentation.Telegram.Hosting;

public sealed class TelegramPollingHostedService(
    UpdateReceiver receiver,
    ITelegramBotClient bot,
    ILogger<TelegramPollingHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

        var me = await bot.GetMe(stoppingToken);
        logger.LogInformation("Бот {Username} успешно запущен. Нажми Enter чтобы остановить.", me.Username);

        try
        {
            await receiver.RunAsync(bot, receiverOptions, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Polling остановлен.");
        }
    }
}