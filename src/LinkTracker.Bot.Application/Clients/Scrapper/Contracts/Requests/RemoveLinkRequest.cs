namespace LinkTracker.Bot.Application.Clients.Scrapper.Contracts.Requests;

public sealed class RemoveLinkRequest
{
    public Uri? Link { get; init; }
}