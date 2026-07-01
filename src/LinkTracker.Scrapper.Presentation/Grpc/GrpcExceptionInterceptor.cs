using System.Net;
using Grpc.Core;
using Grpc.Core.Interceptors;
using LinkTracker.Scrapper.Application.Errors;
using Microsoft.Extensions.Logging;

namespace LinkTracker.Scrapper.Presentation.Grpc;

public sealed class GrpcExceptionInterceptor(ILogger<GrpcExceptionInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (ApiException ex)
        {
            logger.LogWarning(
                ex,
                "gRPC {Method} завершился с ошибкой. Code={Code}",
                context.Method,
                ex.Code);

            throw ToRpcException(ex);
        }
        catch (UriFormatException ex)
        {
            logger.LogWarning(ex, "gRPC {Method}: некорректный формат ссылки.", context.Method);
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Некорректный формат ссылки"));
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "gRPC {Method}: неизвестная ошибка", context.Method);
            throw new RpcException(new Status(StatusCode.Internal, "Внутренняя ошибка scrapper"));
        }
    }

    private static RpcException ToRpcException(ApiException ex)
    {
        var status = ex.StatusCode switch
        {
            HttpStatusCode.BadRequest => StatusCode.InvalidArgument,
            HttpStatusCode.NotFound => StatusCode.NotFound,
            HttpStatusCode.Conflict => StatusCode.AlreadyExists,
            _ => StatusCode.Internal
        };

        var metadata = new Metadata { { "error-code", ex.Code } };
        return new RpcException(new Status(status, ex.Description), metadata);
    }
}