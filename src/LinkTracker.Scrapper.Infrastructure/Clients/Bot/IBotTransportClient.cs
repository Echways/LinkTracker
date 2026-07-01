using LinkTracker.Scrapper.Application.Clients.Bot;
using LinkTracker.Shared.Infrastructure;

namespace LinkTracker.Scrapper.Infrastructure.Clients.Bot;

internal interface IBotTransportClient : IBotClient
{
    TransportKind Transport { get; }
}