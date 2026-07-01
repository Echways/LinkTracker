namespace LinkTracker.Shared.Contracts.Common;

public sealed class ApiErrorResponse
{
    public string Description { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? ExceptionName { get; init; }
    public string? ExceptionMessage { get; init; }
    public IReadOnlyList<string> Stacktrace { get; init; } = [];
}