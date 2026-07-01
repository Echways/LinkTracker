using LinkTracker.Bot.Application.Commands.Helpers;
using LinkTracker.Bot.Application.Dialogs.Implementations.Track;
using LinkTracker.Bot.Application.Dialogs.Runtime;
using LinkTracker.Bot.Application.Models;

namespace LinkTracker.Bot.Application.Commands.Implementations;

public sealed class TrackCommand(DialogManager dialogManager) : ICommandDescriptor, ICommandHandler
{
    public string Name => "track";
    public string Description => "Начать отслеживать ссылку";
    public bool ShowInHelp => true;
    public bool ShowInTelegramMenu => true;

    public bool CanHandle(string text)
    {
        return CommandTextMatcher.Matches(text, Name);
    }

    public async Task<OutgoingMessage> ExecuteAsync(long chatId, string text, CancellationToken ct = default)
    {
        var reply = await dialogManager.StartAsync(TrackDialog.DialogId, chatId, ct);
        return new OutgoingMessage(chatId, reply);
    }
}