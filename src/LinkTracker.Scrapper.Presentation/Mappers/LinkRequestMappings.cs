using LinkTracker.Scrapper.Application.Errors;
using LinkTracker.Scrapper.Contracts.Requests;

namespace LinkTracker.Scrapper.Presentation.Mappers;

public static class LinkRequestMappings
{
    public static (Uri Link, IReadOnlyList<string> Tags, IReadOnlyList<string> Filters) ToAddLinkData(
        this AddLinkRequest? request)
    {
        return request?.Link is null ? throw ScrapperErrors.RequestLinkIsRequired() : (request.Link, request.Tags, request.Filters);
    }

    public static Uri ToRemoveLinkData(this RemoveLinkRequest? request)
    {
        return request?.Link ?? throw ScrapperErrors.RequestLinkIsRequired();
    }
}