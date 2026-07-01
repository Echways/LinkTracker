using LinkTracker.Scrapper.Contracts.Responses;
using LinkTracker.Scrapper.Storage.Abstractions.Models;

namespace LinkTracker.Scrapper.Presentation.Mappers;

public static class TrackedLinkMappings
{
    public static LinkResponse ToResponse(this TrackedLinkRecord record)
    {
        return new LinkResponse { Id = record.Id, Url = record.Url, Tags = record.Tags, Filters = [] };
    }
}