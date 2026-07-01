using System.Net;
using LinkTracker.Shared.Contracts.Common;

namespace LinkTracker.Scrapper.Infrastructure.Clients.Bot;

public sealed class BotClientException(
    HttpStatusCode statusCode,
    string message,
    ApiErrorResponse? error = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public ApiErrorResponse? Error { get; } = error;
}