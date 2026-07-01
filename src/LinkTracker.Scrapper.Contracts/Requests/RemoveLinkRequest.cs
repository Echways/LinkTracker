namespace LinkTracker.Scrapper.Contracts.Requests;

public sealed class RemoveLinkRequest
{
    public Uri? Link { get; init; }
}