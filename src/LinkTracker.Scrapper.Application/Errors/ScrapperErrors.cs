using System.Net;
using LinkTracker.Shared.Contracts.Common;

namespace LinkTracker.Scrapper.Application.Errors;

public static class ScrapperErrors
{
    public static ApiException MissingChatIdHeader()
    {
        return new ApiException(
            HttpStatusCode.BadRequest,
            ScrapperErrorCodes.MissingHeader,
            "Required header 'Tg-Chat-Id' is missing.");
    }

    public static ApiException RequestLinkIsRequired()
    {
        return new ApiException(
            HttpStatusCode.BadRequest,
            ScrapperErrorCodes.InvalidRequest,
            "Field 'link' is required.");
    }

    public static ApiException ChatAlreadyExists(long chatId)
    {
        return new ApiException(
            HttpStatusCode.Conflict,
            ScrapperErrorCodes.ChatAlreadyExists,
            $"Chat with id={chatId} is already registered.");
    }

    public static ApiException ChatNotFound(long chatId)
    {
        return new ApiException(
            HttpStatusCode.NotFound,
            ScrapperErrorCodes.ChatNotFound,
            $"Chat with id={chatId} does not exist.");
    }

    public static ApiException LinkAlreadyExists(Uri link)
    {
        return new ApiException(
            HttpStatusCode.Conflict,
            ScrapperErrorCodes.LinkAlreadyExists,
            $"Link '{link}' is already tracked.");
    }

    public static ApiException LinkNotFound(Uri link)
    {
        return new ApiException(
            HttpStatusCode.NotFound,
            ScrapperErrorCodes.LinkNotFound,
            $"Link '{link}' was not found.");
    }

    public static ApiException InvalidChatId()
    {
        return new ApiException(
            HttpStatusCode.BadRequest,
            ScrapperErrorCodes.InvalidChatId,
            "Chat id must be a positive number.");
    }

    public static ApiException InvalidLink()
    {
        return new ApiException(
            HttpStatusCode.BadRequest,
            ScrapperErrorCodes.InvalidLink,
            "Link must be an absolute URI.");
    }

    public static ApiException InvalidLinkScheme()
    {
        return new ApiException(
            HttpStatusCode.BadRequest,
            ScrapperErrorCodes.InvalidLinkScheme,
            "Only http/https links are supported.");
    }

    public static ApiException UnsupportedLink(Uri link)
    {
        return new ApiException(
            HttpStatusCode.BadRequest,
            ScrapperErrorCodes.UnsupportedLink,
            $"Link '{link}' is not supported. Only GitHub repositories, StackOverflow questions and Reddit subreddits are supported.");
    }
}