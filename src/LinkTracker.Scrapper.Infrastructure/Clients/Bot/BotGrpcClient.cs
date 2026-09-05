using System.Net;
using Grpc.Core;
using LinkTracker.Grpc;
using LinkTracker.Shared.Contracts.Bot;
using LinkTracker.Shared.Infrastructure;
using Microsoft.Extensions.Logging;

namespace LinkTracker.Scrapper.Infrastructure.Clients.Bot;

internal sealed class BotGrpcClient(
    BotUpdatesGrpc.BotUpdatesGrpcClient client,
    ILogger<BotGrpcClient> logger) : IBotTransportClient
{
    public TransportKind Transport => TransportKind.Grpc;

    public async Task SendUpdateAsync(LinkUpdate update, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Bot gRPC client: SendUpdate called. UpdateId={UpdateId}, Url={Url}, ChatsCount={ChatsCount}",
            update.Id,
            update.Url,
            update.TgChatIds.Count);

        try
        {
            var request = new LinkUpdateGrpcRequest { Id = update.Id, Url = update.Url.ToString(), Description = update.Description };

            request.TgChatIds.AddRange(update.TgChatIds);

            await client.SendUpdateAsync(request, cancellationToken: ct);

            logger.LogInformation(
                "Bot gRPC client: SendUpdate succeeded. UpdateId={UpdateId}, ChatsCount={ChatsCount}",
                update.Id,
                update.TgChatIds.Count);
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "Bot gRPC client: SendUpdate failed. UpdateId={UpdateId}", update.Id);
            throw ToClientException(ex);
        }
    }

    private static BotClientException ToClientException(RpcException ex)
    {
        var statusCode = ex.StatusCode switch
        {
            StatusCode.InvalidArgument => HttpStatusCode.BadRequest,
            StatusCode.NotFound => HttpStatusCode.NotFound,
            StatusCode.AlreadyExists => HttpStatusCode.Conflict,
            _ => HttpStatusCode.InternalServerError
        };

        return new BotClientException(statusCode, ex.Status.Detail);
    }
}