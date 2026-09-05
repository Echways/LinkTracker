using LinkTracker.Bot.Application.Updates.Abstractions;
using LinkTracker.Shared.Contracts.Bot;
using LinkTracker.Shared.Contracts.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LinkTracker.Bot.Presentation.BotApi.Endpoints;

public static class LinkUpdateEndpoints
{
    public static RouteGroupBuilder MapBotApi(this IEndpointRouteBuilder builder)
    {
        var app = builder.MapGroup(string.Empty);

        app.MapPost("/updates", HandleUpdateAsync)
            .WithName("HandleUpdate")
            .WithSummary("Send update")
            .WithDescription("Receives a link update from Scrapper and sends it to Telegram.")
            .Accepts<LinkUpdate>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> HandleUpdateAsync(
        [FromBody] LinkUpdate? update,
        [FromServices] ILinkUpdateNotifier notifier,
        CancellationToken ct)
    {
        if (update is null)
        {
            return Results.BadRequest(new ApiErrorResponse { Description = "Request body is required.", Code = "invalid_request" });
        }

        if (update.TgChatIds.Count == 0)
        {
            return Results.BadRequest(new ApiErrorResponse { Description = "Field 'tgChatIds' must contain at least one chat id.", Code = "invalid_request" });
        }

        await notifier.NotifyAsync(update, ct);
        return Results.Ok();
    }
}