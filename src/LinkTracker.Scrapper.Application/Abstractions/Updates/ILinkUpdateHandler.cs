using LinkTracker.Scrapper.Application.Models.Updates;
using LinkTracker.Scrapper.Storage.Abstractions.Models;

namespace LinkTracker.Scrapper.Application.Abstractions.Updates;

public interface ILinkUpdateHandler
{
    bool CanHandle(Uri url);

    Task<LinkCheckResult> CheckAsync(
        TrackedLinkSubscription subscription,
        CancellationToken ct = default);
}