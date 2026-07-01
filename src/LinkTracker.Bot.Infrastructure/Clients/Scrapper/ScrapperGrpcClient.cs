using System.Diagnostics;
using System.Net;
using Grpc.Core;
using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Clients.Scrapper.Contracts.Responses;
using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Grpc;
using LinkTracker.Shared.Contracts.Common;
using Microsoft.Extensions.Logging;

namespace LinkTracker.Bot.Infrastructure.Clients.Scrapper;

public sealed class ScrapperGrpcClient(
    ScrapperGrpc.ScrapperGrpcClient client,
    ILogger<ScrapperGrpcClient> logger,
    IBotMetrics metrics) : IScrapperClient
{
    private const string Scope = "scrapper_async_api";

    public async Task RegisterChatAsync(long chatId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Клиент gRPC Scrapper: вызов RegisterChat. ChatId={ChatId}", chatId);

            try
            {
                await client.RegisterChatAsync(new ChatRequest { ChatId = chatId }, cancellationToken: ct);
                logger.LogInformation("Клиент gRPC Scrapper: RegisterChat успешно выполнен. ChatId={ChatId}", chatId);
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Клиент gRPC Scrapper: RegisterChat завершился с ошибкой. ChatId={ChatId}", chatId);
                throw ToClientException(ex);
            }
        }
        finally
        {
            metrics.ObserveScrapperCallDuration(Scope, nameof(RegisterChatAsync), sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task DeleteChatAsync(long chatId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Клиент gRPC Scrapper: вызов DeleteChat. ChatId={ChatId}", chatId);

            try
            {
                await client.DeleteChatAsync(new ChatRequest { ChatId = chatId }, cancellationToken: ct);
                logger.LogInformation("Клиент gRPC Scrapper: DeleteChat успешно выполнен. ChatId={ChatId}", chatId);
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Клиент gRPC Scrapper: DeleteChat завершился с ошибкой. ChatId={ChatId}", chatId);
                throw ToClientException(ex);
            }
        }
        finally
        {
            metrics.ObserveScrapperCallDuration(Scope, nameof(DeleteChatAsync), sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task<ListLinksResponse> GetLinksAsync(long chatId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Клиент gRPC Scrapper: вызов GetLinks. ChatId={ChatId}", chatId);

            try
            {
                var response = await client.GetLinksAsync(
                    new GetLinksRequest { ChatId = chatId },
                    cancellationToken: ct);

                logger.LogInformation(
                    "Клиент gRPC Scrapper: GetLinks успешно выполнен. ChatId={ChatId}, КоличествоСсылок={Count}",
                    chatId,
                    response.Size);

                return new ListLinksResponse { Size = response.Size, Links = response.Links.Select(ToModel).ToArray() };
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Клиент gRPC Scrapper: GetLinks завершился с ошибкой. ChatId={ChatId}", chatId);
                throw ToClientException(ex);
            }
        }
        finally
        {
            metrics.ObserveScrapperCallDuration(Scope, nameof(GetLinksAsync), sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task<LinkResponse> AddLinkAsync(
        long chatId,
        Uri link,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> filters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            logger.LogInformation(
                "Клиент gRPC Scrapper: вызов AddLink. ChatId={ChatId}, Ссылка={Link}, КоличествоТегов={TagsCount}, КоличествоФильтров={FiltersCount}",
                chatId,
                link,
                tags.Count,
                filters.Count);

            try
            {
                var request = new AddLinkGrpcRequest { ChatId = chatId, Link = link.ToString() };

                request.Tags.AddRange(tags);

                var response = await client.AddLinkAsync(request, cancellationToken: ct);

                logger.LogInformation(
                    "Клиент gRPC Scrapper: AddLink успешно выполнен. ChatId={ChatId}, LinkId={LinkId}",
                    chatId,
                    response.Id);

                return ToModel(response);
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Клиент gRPC Scrapper: AddLink завершился с ошибкой. ChatId={ChatId}, Ссылка={Link}", chatId, link);
                throw ToClientException(ex);
            }
        }
        finally
        {
            metrics.ObserveScrapperCallDuration(Scope, nameof(AddLinkAsync), sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task<LinkResponse> RemoveLinkAsync(long chatId, Uri link, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Клиент gRPC Scrapper: вызов RemoveLink. ChatId={ChatId}, Ссылка={Link}", chatId, link);

            try
            {
                var response = await client.RemoveLinkAsync(
                    new RemoveLinkGrpcRequest { ChatId = chatId, Link = link.ToString() },
                    cancellationToken: ct);

                logger.LogInformation(
                    "Клиент gRPC Scrapper: RemoveLink успешно выполнен. ChatId={ChatId}, LinkId={LinkId}",
                    chatId,
                    response.Id);

                return ToModel(response);
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Клиент gRPC Scrapper: RemoveLink завершился с ошибкой. ChatId={ChatId}, Ссылка={Link}", chatId, link);
                throw ToClientException(ex);
            }
        }
        finally
        {
            metrics.ObserveScrapperCallDuration(Scope, nameof(RemoveLinkAsync), sw.Elapsed.TotalMilliseconds);
        }
    }

    private static LinkResponse ToModel(LinkGrpcResponse response)
    {
        return new LinkResponse { Id = response.Id, Url = new Uri(response.Url), Tags = response.Tags.ToArray(), Filters = [] };
    }

    private static ScrapperClientException ToClientException(RpcException ex)
    {
        var (statusCode, fallbackCode) = MapStatus(ex.StatusCode);

        var errorCode = ex.Trailers
            .FirstOrDefault(x => string.Equals(x.Key, "error-code", StringComparison.OrdinalIgnoreCase))
            ?.Value ?? fallbackCode;

        return new ScrapperClientException(statusCode, ex.Status.Detail, innerException: ex) { FallbackCode = errorCode };
    }

    private static (HttpStatusCode StatusCode, string? FallbackCode) MapStatus(StatusCode statusCode)
    {
        return statusCode switch
        {
            StatusCode.InvalidArgument => (HttpStatusCode.BadRequest, null),
            StatusCode.NotFound => (HttpStatusCode.NotFound, null),
            StatusCode.AlreadyExists => (HttpStatusCode.Conflict, null),
            StatusCode.Unavailable => (HttpStatusCode.ServiceUnavailable, ScrapperErrorCodes.ScrapperServiceUnavailable),
            StatusCode.DeadlineExceeded => (HttpStatusCode.GatewayTimeout, ScrapperErrorCodes.ScrapperServiceUnavailable),
            _ => (HttpStatusCode.InternalServerError, null)
        };
    }
}