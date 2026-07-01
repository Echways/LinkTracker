using System.Net;

namespace LinkTracker.Scrapper.Application.Errors;

public sealed class ApiException(HttpStatusCode statusCode, string code, string description) : Exception(description)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    public string Description { get; } = description;
}