using LinkTracker.Shared.Contracts.Common;

namespace LinkTracker.Scrapper.Infrastructure.Errors;

public static class ApiErrorResponseFactory
{
    public static ApiErrorResponse Create(
        string description,
        string code,
        Exception? exception = null,
        bool includeExceptionDetails = false)
    {
        if (exception is null || !includeExceptionDetails)
        {
            return new ApiErrorResponse { Description = description, Code = code };
        }

        return new ApiErrorResponse
        {
            Description = description,
            Code = code,
            ExceptionName = exception.GetType().Name,
            ExceptionMessage = exception.Message,
            Stacktrace = exception.StackTrace?.Split(Environment.NewLine) ?? []
        };
    }
}
