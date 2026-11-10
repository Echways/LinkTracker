using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Models;

namespace LinkTracker.Bot.Application.Callbacks;

public interface ICallbackHandler
{
    bool CanHandle(string callbackData);
    Task<OutgoingMessage> HandleAsync(BotRequest request, CancellationToken ct);
}
