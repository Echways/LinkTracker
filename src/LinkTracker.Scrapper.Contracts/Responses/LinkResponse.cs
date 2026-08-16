namespace LinkTracker.Scrapper.Contracts.Responses;

public sealed class LinkResponse
{
    public long Id { get; init; }

    public Uri Url { get; init; } = default!;

    public IReadOnlyList<string> Tags { get; init; } = [];
}