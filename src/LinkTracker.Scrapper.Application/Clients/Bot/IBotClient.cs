using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Scrapper.Application.Clients.Bot;

public interface IBotClient
{
    Task SendUpdateAsync(LinkUpdate update, CancellationToken ct = default);
}