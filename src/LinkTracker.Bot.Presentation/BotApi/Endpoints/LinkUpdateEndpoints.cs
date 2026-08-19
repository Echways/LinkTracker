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
            .WithSummary("Отправить обновление")
            .WithDescription("Получает обновление ссылки от Scrapper и отправляет его в телеграм")
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
            return Results.BadRequest(new ApiErrorResponse { Description = "Тело запроса обязательно.", Code = "invalid_request" });
        }

        if (update.TgChatIds.Count == 0)
        {
            return Results.BadRequest(new ApiErrorResponse { Description = "Поле 'tgChatIds' должно содержать хотя бы один chat id.", Code = "invalid_request" });
        }

        await notifier.NotifyAsync(update, ct);
        return Results.Ok();
    }
}