using System.Net;
using LinkTracker.Scrapper.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LinkTracker.Scrapper.Infrastructure.Errors;

public static class ApiExceptionHandler
{
    private const string LoggerName = "LinkTracker.Scrapper.Api.Errors";

    public static async Task HandleAsync(HttpContext context)
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
        var includeExceptionDetails = environment.IsDevelopment();

        var (statusCode, response) = exception switch
        {
            ApiException apiException => (
                (int)apiException.StatusCode,
                ApiErrorResponseFactory.Create(
                    apiException.Description,
                    apiException.Code,
                    apiException,
                    includeExceptionDetails)),

            _ => (
                (int)HttpStatusCode.InternalServerError,
                ApiErrorResponseFactory.Create(
                    "Internal server error.",
                    "internal_error",
                    exception,
                    includeExceptionDetails))
        };

        if (exception is not null and not ApiException)
        {
            context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(LoggerName)
                .LogError(
                    exception,
                    "Unhandled error while processing request {Method} {Path}.",
                    context.Request.Method,
                    context.Request.Path);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(response);
    }
}
