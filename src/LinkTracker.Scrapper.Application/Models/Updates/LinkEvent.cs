namespace LinkTracker.Scrapper.Application.Models.Updates;

public sealed class LinkEvent
{
    public LinkSourceKind SourceKind { get; init; }
    public LinkEventKind EventKind { get; init; }
    public string Title { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public string EventKey { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public Uri? ResourceUrl { get; init; }
}