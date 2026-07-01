namespace LinkTracker.Scrapper.Contracts.Requests;

public sealed class AddLinkRequest
{
    public Uri? Link { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<string> Filters { get; init; } = [];
}