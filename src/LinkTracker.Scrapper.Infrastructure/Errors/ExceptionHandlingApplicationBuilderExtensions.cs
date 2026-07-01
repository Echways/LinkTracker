using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LinkTracker.Scrapper.Infrastructure.Errors;

public static class ExceptionHandlingApplicationBuilderExtensions
{
    public static IApplicationBuilder UseScrapperExceptionHandling(this IApplicationBuilder app)
    {
        app.UseWhen(
            static context => IsGrpcRequest(context) is false,
            static appBuilder => appBuilder.UseExceptionHandler(errorApp => errorApp.Run(ApiExceptionHandler.HandleAsync)));

        return app;
    }

    private static bool IsGrpcRequest(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/scrapper.ScrapperGrpc", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return context.Request.Protocol == "HTTP/2"
               && context.Request.ContentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true;
    }
}