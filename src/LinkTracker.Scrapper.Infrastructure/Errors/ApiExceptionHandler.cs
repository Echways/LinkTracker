using System.Net;
using LinkTracker.Scrapper.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace LinkTracker.Scrapper.Infrastructure.Errors;

public static class ApiExceptionHandler
{
    public static async Task HandleAsync(HttpContext context)
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        var (statusCode, response) = exception switch
        {
            ApiException apiException => (
                (int)apiException.StatusCode,
                ApiErrorResponseFactory.Create(
                    apiException.Description,
                    apiException.Code,
                    apiException)),

            _ => (
                (int)HttpStatusCode.InternalServerError,
                ApiErrorResponseFactory.Create(
                    "Внутренняя ошибка сервера.",
                    "internal_error",
                    exception))
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(response);
    }
}