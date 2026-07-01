using LinkTracker.Bot.Application.Commands.Registration;
using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Runtime;
using LinkTracker.Bot.Application.Models;
using LinkTracker.Bot.Application.Telemetry.Abstractions;

namespace LinkTracker.Bot.Application.Routing;

public sealed class UpdateRouter(
    CommandRouter commandRouter,
    DialogManager dialogManager,
    IBotMetrics metrics)
{
    public async Task<OutgoingMessage> RouteAsync(BotRequest request, CancellationToken ct)
    {
        metrics.IncrementRequest(request.Type.ToString());

        if (IsCommand(request))
        {
            await dialogManager.ResetAsync(request.ChatId, ct);
            return await RouteCommandAsync(request, ct);
        }

        var (handled, replyText) = await dialogManager.TryHandleAsync(request, ct);
        if (handled)
        {
            return new OutgoingMessage(request.ChatId, replyText);
        }

        return await RouteCommandAsync(request, ct);
    }

    private async Task<OutgoingMessage> RouteCommandAsync(BotRequest request, CancellationToken ct)
    {
        var text = request.Text ?? ToCommandText(request);
        return await commandRouter.RouteAsync(request.ChatId, text, ct);
    }

    private static bool IsCommand(BotRequest request)
    {
        return request is { Type: BotRequestType.Command, Command: not null };
    }

    private static string ToCommandText(BotRequest request)
    {
        return request.Command is null ? string.Empty : $"/{request.Command}";
    }
}