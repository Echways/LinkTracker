using LinkTracker.Bot.Application.Callbacks;
using LinkTracker.Bot.Application.Commands.Registration;
using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Runtime;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Application.Models;
using LinkTracker.Bot.Application.Telemetry.Abstractions;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Routing;

public sealed class UpdateRouter(
    CommandRouter commandRouter,
    CallbackRouter callbackRouter,
    DialogManager dialogManager,
    ChatLanguageService languages,
    IStringLocalizer<Messages> localizer,
    IBotMetrics metrics)
{
    public async Task<OutgoingMessage> RouteAsync(BotRequest request, CancellationToken ct)
    {
        metrics.IncrementRequest(request.Type.ToString());

        var language = await languages.GetAsync(request.ChatId, ct);
        using var _ = UiCulture.Use(language);

        if (request.Type == BotRequestType.Callback)
        {
            return await callbackRouter.RouteAsync(request, ct);
        }

        if (IsCommand(request))
        {
            var dialogInterrupted = await dialogManager.ResetAsync(request.ChatId, ct);
            var message = await RouteCommandAsync(request, ct);

            return dialogInterrupted
                ? message with { Text = $"{localizer[Texts.DialogInterruptedNotice]}{Environment.NewLine}{Environment.NewLine}{message.Text}" }
                : message;
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
