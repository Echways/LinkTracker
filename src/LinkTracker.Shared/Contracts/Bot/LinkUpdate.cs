namespace LinkTracker.Shared.Contracts.Bot;

public sealed class LinkUpdate
{
    public long Id { get; init; }

    public Uri Url { get; init; } = default!;

    public string Description { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public IReadOnlyList<long> TgChatIds { get; init; } = [];
}