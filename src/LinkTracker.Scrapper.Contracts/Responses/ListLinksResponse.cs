namespace LinkTracker.Scrapper.Contracts.Responses;

public sealed class ListLinksResponse
{
    public IReadOnlyList<LinkResponse> Links { get; init; } = [];

    public int Size { get; init; }
}