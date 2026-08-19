using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Options;

namespace LinkTracker.Shared.Infrastructure.Authentication;

public sealed class ServiceAuthClientInterceptor(IOptions<ServiceAuthOptions> options) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(request, WithServiceToken(context));
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(request, WithServiceToken(context));
    }

    private ClientInterceptorContext<TRequest, TResponse> WithServiceToken<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var headers = context.Options.Headers ?? [];

        headers.Add(ServiceAuthDefaults.HeaderName, options.Value.Secret);

        return new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(headers));
    }
}
