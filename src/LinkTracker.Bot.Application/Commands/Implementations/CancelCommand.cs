using LinkTracker.Bot.Application.Commands.Helpers;
using LinkTracker.Bot.Application.Dialogs.Runtime;
using LinkTracker.Bot.Application.Models;

namespace LinkTracker.Bot.Application.Commands.Implementations;

public sealed class CancelCommand(DialogManager dialogManager) : ICommandDescriptor, ICommandHandler
{
    public string Name => "cancel";
    public string Description => "Отменить текущий диалог";
    public bool ShowInHelp => true;
    public bool ShowInTelegramMenu => false;

    public bool CanHandle(string text)
    {
        return CommandTextMatcher.Matches(text, Name);
    }

    public async Task<OutgoingMessage> ExecuteAsync(long chatId, string text, CancellationToken ct = default)
    {
        var reply = await dialogManager.CancelAsync(chatId, ct);
        return new OutgoingMessage(chatId, reply);
    }
}