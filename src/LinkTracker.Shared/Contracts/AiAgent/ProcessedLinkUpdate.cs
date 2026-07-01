namespace LinkTracker.Shared.Contracts.AiAgent;

public sealed class ProcessedLinkUpdate
{
    public long Id { get; init; }

    public Uri Url { get; init; } = default!;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<long> TgChatIds { get; init; } = [];

    public LinkUpdatePriority Priority { get; init; } = LinkUpdatePriority.Medium;
}