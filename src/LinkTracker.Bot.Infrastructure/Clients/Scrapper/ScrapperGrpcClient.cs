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
            logger.LogInformation("Scrapper gRPC client: RegisterChat called. ChatId={ChatId}", chatId);

            try
            {
                await client.RegisterChatAsync(new ChatRequest { ChatId = chatId }, cancellationToken: ct);
                logger.LogInformation("Scrapper gRPC client: RegisterChat succeeded. ChatId={ChatId}", chatId);
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Scrapper gRPC client: RegisterChat failed. ChatId={ChatId}", chatId);
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
            logger.LogInformation("Scrapper gRPC client: DeleteChat called. ChatId={ChatId}", chatId);

            try
            {
                await client.DeleteChatAsync(new ChatRequest { ChatId = chatId }, cancellationToken: ct);
                logger.LogInformation("Scrapper gRPC client: DeleteChat succeeded. ChatId={ChatId}", chatId);
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Scrapper gRPC client: DeleteChat failed. ChatId={ChatId}", chatId);
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
            logger.LogInformation("Scrapper gRPC client: GetLinks called. ChatId={ChatId}", chatId);

            try
            {
                var response = await client.GetLinksAsync(
                    new GetLinksRequest { ChatId = chatId },
                    cancellationToken: ct);

                logger.LogInformation(
                    "Scrapper gRPC client: GetLinks succeeded. ChatId={ChatId}, LinksCount={Count}",
                    chatId,
                    response.Size);

                return new ListLinksResponse { Size = response.Size, Links = response.Links.Select(ToModel).ToArray() };
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Scrapper gRPC client: GetLinks failed. ChatId={ChatId}", chatId);
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
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            logger.LogInformation(
                "Scrapper gRPC client: AddLink called. ChatId={ChatId}, Link={Link}, TagsCount={TagsCount}",
                chatId,
                link,
                tags.Count);

            try
            {
                var request = new AddLinkGrpcRequest { ChatId = chatId, Link = link.ToString() };

                request.Tags.AddRange(tags);

                var response = await client.AddLinkAsync(request, cancellationToken: ct);

                logger.LogInformation(
                    "Scrapper gRPC client: AddLink succeeded. ChatId={ChatId}, LinkId={LinkId}",
                    chatId,
                    response.Id);

                return ToModel(response);
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Scrapper gRPC client: AddLink failed. ChatId={ChatId}, Link={Link}", chatId, link);
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
            logger.LogInformation("Scrapper gRPC client: RemoveLink called. ChatId={ChatId}, Link={Link}", chatId, link);

            try
            {
                var response = await client.RemoveLinkAsync(
                    new RemoveLinkGrpcRequest { ChatId = chatId, Link = link.ToString() },
                    cancellationToken: ct);

                logger.LogInformation(
                    "Scrapper gRPC client: RemoveLink succeeded. ChatId={ChatId}, LinkId={LinkId}",
                    chatId,
                    response.Id);

                return ToModel(response);
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Scrapper gRPC client: RemoveLink failed. ChatId={ChatId}, Link={Link}", chatId, link);
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
        return new LinkResponse { Id = response.Id, Url = new Uri(response.Url), Tags = response.Tags.ToArray() };
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