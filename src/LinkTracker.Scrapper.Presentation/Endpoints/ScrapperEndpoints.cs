using LinkTracker.Scrapper.Application.Abstractions.Cache;
using LinkTracker.Scrapper.Application.Abstractions.Tracking;
using LinkTracker.Scrapper.Contracts.Requests;
using LinkTracker.Scrapper.Contracts.Responses;
using LinkTracker.Scrapper.Presentation.Headers;
using LinkTracker.Scrapper.Presentation.Mappers;
using LinkTracker.Shared.Contracts.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LinkTracker.Scrapper.Presentation.Endpoints;

public static class ScrapperEndpoints
{
    public static IEndpointRouteBuilder MapScrapperEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/tg-chat/{id:long}", RegisterChat)
            .WithName("RegisterChat")
            .WithSummary("Register chat")
            .WithDescription("Registers a Telegram chat for link tracking.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);

        app.MapDelete("/tg-chat/{id:long}", DeleteChat)
            .WithName("DeleteChat")
            .WithSummary("Delete chat")
            .WithDescription("Deletes a registered Telegram chat.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapGet("/links", GetLinks)
            .WithName("GetLinks")
            .WithSummary("Get tracked links")
            .WithDescription("Returns tracked links for the specified Telegram chat.")
            .Produces<ListLinksResponse>()
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPost("/links", AddLink)
            .WithName("AddLink")
            .WithSummary("Add tracked link")
            .WithDescription("Adds a new tracked link for the specified Telegram chat.")
            .Accepts<AddLinkRequest>("application/json")
            .Produces<LinkResponse>()
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);

        app.MapDelete("/links", RemoveLink)
            .WithName("RemoveLink")
            .WithSummary("Remove tracked link")
            .WithDescription("Removes a tracked link for the specified Telegram chat.")
            .Accepts<RemoveLinkRequest>("application/json")
            .Produces<LinkResponse>()
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RegisterChat(
        [FromRoute] long id,
        [FromServices] ILinkTrackingService service,
        CancellationToken ct)
    {
        await service.RegisterChatAsync(id, ct);
        return Results.Ok();
    }

    private static async Task<IResult> DeleteChat(
        [FromRoute] long id,
        [FromServices] ILinkTrackingService service,
        [FromServices] ILinksResponseCache cache,
        CancellationToken ct)
    {
        await service.DeleteChatAsync(id, ct);
        await cache.InvalidateAsync(id, ct);

        return Results.Ok();
    }

    private static async Task<IResult> GetLinks(
        [FromHeader(Name = "Tg-Chat-Id")] long? chatId,
        [FromServices] ILinkTrackingService service,
        [FromServices] ILinksResponseCache cache,
        CancellationToken ct)
    {
        var resolvedChatId = TgChatId.Require(chatId);

        var response = await cache.GetOrCreateAsync(
            resolvedChatId,
            async token =>
            {
                var links = await service.GetLinksAsync(resolvedChatId, token);

                return new ListLinksResponse { Links = links.Select(x => x.ToResponse()).ToArray(), Size = links.Count };
            },
            ct);

        return Results.Ok(response);
    }

    private static async Task<IResult> AddLink(
        [FromHeader(Name = "Tg-Chat-Id")] long? chatId,
        [FromBody] AddLinkRequest? request,
        [FromServices] ILinkTrackingService service,
        [FromServices] ILinksResponseCache cache,
        CancellationToken ct)
    {
        var resolvedChatId = TgChatId.Require(chatId);
        var data = request.ToAddLinkData();

        var record = await service.AddLinkAsync(
            resolvedChatId,
            data.Link,
            data.Tags,
            ct);

        await cache.InvalidateAsync(resolvedChatId, ct);

        return Results.Ok(record.ToResponse());
    }

    private static async Task<IResult> RemoveLink(
        [FromHeader(Name = "Tg-Chat-Id")] long? chatId,
        [FromBody] RemoveLinkRequest? request,
        [FromServices] ILinkTrackingService service,
        [FromServices] ILinksResponseCache cache,
        CancellationToken ct)
    {
        var resolvedChatId = TgChatId.Require(chatId);
        var link = request.ToRemoveLinkData();

        var record = await service.RemoveLinkAsync(resolvedChatId, link, ct);

        await cache.InvalidateAsync(resolvedChatId, ct);

        return Results.Ok(record.ToResponse());
    }
}