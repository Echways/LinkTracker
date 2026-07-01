using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Scrapper.Infrastructure.Outbox.Abstractions;

internal interface IOutboxMessageSerializer
{
    string Serialize(LinkUpdate update);

    LinkUpdate? Deserialize(string payload);
}