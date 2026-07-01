using LinkTracker.Scrapper.Application.Models.Updates;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Scrapper.Application.Services.Updates;

public static class LinkUpdatePayloadMapper
{
    public static LinkUpdate ToBotUpdate(
        TrackedLinkSubscription subscription,
        LinkEvent linkEvent)
    {
        return new LinkUpdate
        {
            Id = subscription.Id,
            Url = subscription.Url,
            Description = LinkEventDescriptionFormatter.Format(linkEvent),
            Author = linkEvent.UserName,
            TgChatIds = subscription.TgChatIds
        };
    }
}