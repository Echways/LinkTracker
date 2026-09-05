using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LinkTracker.Bot.Application.Updates.Abstractions;
using LinkTracker.Grpc;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Logging;

namespace LinkTracker.Bot.Presentation.Grpc;

public sealed class BotUpdatesGrpcService(
    ILinkUpdateNotifier notifier,
    ILogger<BotUpdatesGrpcService> logger)
    : BotUpdatesGrpc.BotUpdatesGrpcBase
{
    public override async Task<Empty> SendUpdate(LinkUpdateGrpcRequest request, ServerCallContext context)
    {
        logger.LogInformation(
            "gRPC SendUpdate called. UpdateId={UpdateId}, Url={Url}, ChatsCount={ChatsCount}",
            request.Id,
            request.Url,
            request.TgChatIds.Count);

        if (request.TgChatIds.Count == 0)
        {
            logger.LogWarning(
                "gRPC SendUpdate rejected: tg_chat_ids is empty. UpdateId={UpdateId}",
                request.Id);

            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "tg_chat_ids must not be empty"));
        }

        try
        {
            await notifier.NotifyAsync(
                new LinkUpdate { Id = request.Id, Url = new Uri(request.Url), Description = request.Description, TgChatIds = request.TgChatIds.ToArray() },
                context.CancellationToken);

            logger.LogInformation(
                "gRPC SendUpdate succeeded. UpdateId={UpdateId}, ChatsCount={ChatsCount}",
                request.Id,
                request.TgChatIds.Count);

            return new Empty();
        }
        catch (UriFormatException ex)
        {
            logger.LogWarning(
                ex,
                "gRPC SendUpdate rejected: invalid link format. UpdateId={UpdateId}, Url={Url}",
                request.Id,
                request.Url);

            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Invalid link format"));
        }
    }
}