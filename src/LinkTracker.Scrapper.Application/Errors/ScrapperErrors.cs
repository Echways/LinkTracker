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
            "Отсутствует обязательный заголовок 'Tg-Chat-Id'.");
    }

    public static ApiException RequestLinkIsRequired()
    {
        return new ApiException(
            HttpStatusCode.BadRequest,
            ScrapperErrorCodes.InvalidRequest,
            "Поле 'link' обязательно.");
    }

    public static ApiException ChatAlreadyExists(long chatId)
    {
        return new ApiException(
            HttpStatusCode.Conflict,
            ScrapperErrorCodes.ChatAlreadyExists,
            $"Чат с id={chatId} уже зарегистрирован.");
    }

    public static ApiException ChatNotFound(long chatId)
    {
        return new ApiException(
            HttpStatusCode.NotFound,
            ScrapperErrorCodes.ChatNotFound,
            $"Чат с id={chatId} не существует.");
    }

    public static ApiException LinkAlreadyExists(Uri link)
    {
        return new ApiException(
            HttpStatusCode.Conflict,
            ScrapperErrorCodes.LinkAlreadyExists,
            $"Ссылка '{link}' уже отслеживается.");
    }

    public static ApiException LinkNotFound(Uri link)
    {
        return new ApiException(
            HttpStatusCode.NotFound,
            ScrapperErrorCodes.LinkNotFound,
            $"Ссылка '{link}' не найдена.");
    }

    public static ApiException InvalidChatId()
    {
        return new ApiException(
            HttpStatusCode.BadRequest,
            ScrapperErrorCodes.InvalidChatId,
            "Идентификатор чата должен быть положительным числом.");
    }

    public static ApiException InvalidLink()
    {
        return new ApiException(
            HttpStatusCode.BadRequest,
            ScrapperErrorCodes.InvalidLink,
            "Ссылка должна быть абсолютным URI.");
    }

    public static ApiException InvalidLinkScheme()
    {
        return new ApiException(
            HttpStatusCode.BadRequest,
            ScrapperErrorCodes.InvalidLinkScheme,
            "Поддерживаются только ссылки с http/https.");
    }

    public static ApiException UnsupportedLink(Uri link)
    {
        return new ApiException(
            HttpStatusCode.BadRequest,
            ScrapperErrorCodes.UnsupportedLink,
            $"Ссылка '{link}' не поддерживается. Сейчас поддерживаются только GitHub repository и StackOverflow question.");
    }
}