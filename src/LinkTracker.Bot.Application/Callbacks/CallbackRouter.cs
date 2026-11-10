using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Application.Models;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Callbacks;

public sealed class CallbackRouter(
    IEnumerable<ICallbackHandler> handlers,
    IStringLocalizer<Messages> localizer)
{
    private readonly IReadOnlyList<ICallbackHandler> _handlers = handlers.ToList();

    public async Task<OutgoingMessage> RouteAsync(BotRequest request, CancellationToken ct)
    {
        var handler = request.CallbackData is null
            ? null
            : _handlers.FirstOrDefault(h => h.CanHandle(request.CallbackData));

        return handler is null
            ? new OutgoingMessage(request.ChatId, localizer[Texts.CommandNotUnderstood])
            : await handler.HandleAsync(request, ct);
    }
}
