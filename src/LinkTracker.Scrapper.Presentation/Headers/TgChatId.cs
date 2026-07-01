using LinkTracker.Scrapper.Application.Errors;

namespace LinkTracker.Scrapper.Presentation.Headers;

public static class TgChatId
{
    public static long Require(long? chatId)
    {
        return chatId ?? throw ScrapperErrors.MissingChatIdHeader();
    }
}