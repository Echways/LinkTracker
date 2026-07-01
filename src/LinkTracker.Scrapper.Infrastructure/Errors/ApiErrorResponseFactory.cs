using LinkTracker.Shared.Contracts.Common;

namespace LinkTracker.Scrapper.Infrastructure.Errors;

public static class ApiErrorResponseFactory
{
    public static ApiErrorResponse Create(
        string description,
        string code,
        Exception? exception = null)
    {
        return new ApiErrorResponse
        {
            Description = description,
            Code = code,
            ExceptionName = exception?.GetType().Name,
            ExceptionMessage = exception?.Message,
            Stacktrace = exception?.StackTrace?.Split(Environment.NewLine) ?? Array.Empty<string>()
        };
    }
}