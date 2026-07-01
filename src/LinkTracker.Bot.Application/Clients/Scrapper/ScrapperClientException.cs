using System.Net;
using LinkTracker.Shared.Contracts.Common;

namespace LinkTracker.Bot.Application.Clients.Scrapper;

public sealed class ScrapperClientException(
    HttpStatusCode statusCode,
    string message,
    ApiErrorResponse? error = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public ApiErrorResponse? Error { get; } = error;

    public string? FallbackCode { get; init; }

    public string? ErrorCode => Error?.Code ?? FallbackCode;

    public string? ErrorDescription => Error?.Description;

    public bool HasCode(string code)
    {
        return string.Equals(ErrorCode, code, StringComparison.OrdinalIgnoreCase);
    }
}