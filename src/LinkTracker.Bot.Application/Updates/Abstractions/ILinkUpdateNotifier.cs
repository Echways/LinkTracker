using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Bot.Application.Updates.Abstractions;

public interface ILinkUpdateNotifier
{
    Task NotifyAsync(LinkUpdate update, CancellationToken ct = default);
}