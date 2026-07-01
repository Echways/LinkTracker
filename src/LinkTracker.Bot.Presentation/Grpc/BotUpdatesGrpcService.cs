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
            "gRPC вызов SendUpdate. UpdateId={UpdateId}, Ссылка={Url}, КоличествоЧатов={ChatsCount}",
            request.Id,
            request.Url,
            request.TgChatIds.Count);

        if (request.TgChatIds.Count == 0)
        {
            logger.LogWarning(
                "gRPC SendUpdate отклонён: список tg_chat_ids пуст. UpdateId={UpdateId}",
                request.Id);

            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Список tg_chat_ids не должен быть пустым"));
        }

        try
        {
            await notifier.NotifyAsync(
                new LinkUpdate { Id = request.Id, Url = new Uri(request.Url), Description = request.Description, TgChatIds = request.TgChatIds.ToArray() },
                context.CancellationToken);

            logger.LogInformation(
                "gRPC SendUpdate успешно выполнен. UpdateId={UpdateId}, КоличествоЧатов={ChatsCount}",
                request.Id,
                request.TgChatIds.Count);

            return new Empty();
        }
        catch (UriFormatException ex)
        {
            logger.LogWarning(
                ex,
                "gRPC SendUpdate отклонён: некорректный формат ссылки. UpdateId={UpdateId}, Url={Url}",
                request.Id,
                request.Url);

            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Некорректный формат ссылки"));
        }
    }
}