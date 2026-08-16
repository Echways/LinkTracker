namespace LinkTracker.Bot.Application.Clients.Scrapper.Contracts.Requests;

public sealed class AddLinkRequest
{
    public Uri? Link { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}