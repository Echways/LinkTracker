using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LinkTracker.Grpc;
using LinkTracker.Scrapper.Application.Abstractions.Cache;
using LinkTracker.Scrapper.Application.Abstractions.Tracking;
using LinkTracker.Scrapper.Contracts.Responses;
using LinkTracker.Scrapper.Presentation.Mappers;
using LinkTracker.Scrapper.Storage.Abstractions.Models;

namespace LinkTracker.Scrapper.Presentation.Grpc;

public sealed class ScrapperGrpcService(
    ILinkTrackingService trackingService,
    ILinksResponseCache cache)
    : ScrapperGrpc.ScrapperGrpcBase
{
    public override async Task<Empty> RegisterChat(ChatRequest request, ServerCallContext context)
    {
        await trackingService.RegisterChatAsync(request.ChatId, context.CancellationToken);
        return new Empty();
    }

    public override async Task<Empty> DeleteChat(ChatRequest request, ServerCallContext context)
    {
        await trackingService.DeleteChatAsync(request.ChatId, context.CancellationToken);
        await cache.InvalidateAsync(request.ChatId, context.CancellationToken);

        return new Empty();
    }

    public override async Task<ListLinksGrpcResponse> GetLinks(GetLinksRequest request, ServerCallContext context)
    {
        var response = await cache.GetOrCreateAsync(
            request.ChatId,
            async token =>
            {
                var links = await trackingService.GetLinksAsync(request.ChatId, token);

                return new ListLinksResponse { Links = links.Select(x => x.ToResponse()).ToArray(), Size = links.Count };
            },
            context.CancellationToken);

        return ToGrpc(response);
    }

    public override async Task<LinkGrpcResponse> AddLink(AddLinkGrpcRequest request, ServerCallContext context)
    {
        var record = await trackingService.AddLinkAsync(
            request.ChatId,
            new Uri(request.Link),
            request.Tags.ToArray(),
            context.CancellationToken);

        await cache.InvalidateAsync(request.ChatId, context.CancellationToken);

        return ToGrpc(record);
    }

    public override async Task<LinkGrpcResponse> RemoveLink(RemoveLinkGrpcRequest request, ServerCallContext context)
    {
        var record = await trackingService.RemoveLinkAsync(
            request.ChatId,
            new Uri(request.Link),
            context.CancellationToken);

        await cache.InvalidateAsync(request.ChatId, context.CancellationToken);

        return ToGrpc(record);
    }

    private static ListLinksGrpcResponse ToGrpc(ListLinksResponse response)
    {
        var grpcResponse = new ListLinksGrpcResponse { Size = response.Size };

        grpcResponse.Links.AddRange(response.Links.Select(ToGrpc));

        return grpcResponse;
    }

    private static LinkGrpcResponse ToGrpc(LinkResponse response)
    {
        var grpcResponse = new LinkGrpcResponse { Id = response.Id, Url = response.Url.ToString() };

        grpcResponse.Tags.AddRange(response.Tags);

        return grpcResponse;
    }

    private static LinkGrpcResponse ToGrpc(TrackedLinkRecord record)
    {
        return ToGrpc(record.ToResponse());
    }
}