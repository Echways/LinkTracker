using LinkTracker.Shared.Contracts.AiAgent;

namespace LinkTracker.Shared.Contracts.Bot;

public sealed class LinkUpdate
{
    public long Id { get; init; }

    public Uri Url { get; init; } = default!;

    public string Description { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public IReadOnlyList<long> TgChatIds { get; init; } = [];

    /// <summary>
    /// Проставляется AI-агентом. На сыром пути Scrapper -> Bot остаётся Medium:
    /// приоритет там просто ещё не вычислен.
    /// </summary>
    public LinkUpdatePriority Priority { get; init; } = LinkUpdatePriority.Medium;

    public LinkUpdateKind Kind { get; init; } = LinkUpdateKind.Content;
}