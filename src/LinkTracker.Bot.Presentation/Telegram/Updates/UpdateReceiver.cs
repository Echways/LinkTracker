using Telegram.Bot;
using Telegram.Bot.Polling;

namespace LinkTracker.Bot.Presentation.Telegram.Updates;

public sealed class UpdateReceiver(UpdateMapper updateMapper)
{
    public Task RunAsync(ITelegramBotClient botClient, ReceiverOptions receiverOptions, CancellationToken ct)
    {
        return botClient.ReceiveAsync(
            updateMapper.HandleUpdateAsync,
            updateMapper.HandleErrorAsync,
            receiverOptions,
            ct
        );
    }
}