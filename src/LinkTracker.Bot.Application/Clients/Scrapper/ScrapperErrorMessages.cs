using LinkTracker.Shared.Contracts.Common;

namespace LinkTracker.Bot.Application.Clients.Scrapper;

internal static class ScrapperErrorMessages
{
    internal static readonly IReadOnlyDictionary<string, string> ByCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ScrapperErrorCodes.UnsupportedLink] =
                "Эта ссылка не поддерживается.\n" +
                "Сейчас можно отслеживать только:\n" +
                "• GitHub-репозитории: https://github.com/owner/repo\n" +
                "• вопросы StackOverflow: https://stackoverflow.com/questions/12345/...",
            [ScrapperErrorCodes.ChatAlreadyExists] =
                "Привет! Чат уже зарегистрирован. Используй справку /help для просмотра команд",
            [ScrapperErrorCodes.LinkAlreadyExists] =
                "Эта ссылка уже отслеживается.",
            [ScrapperErrorCodes.LinkNotFound] =
                "Эта ссылка не найдена в отслеживаемых.",
            [ScrapperErrorCodes.ChatNotFound] =
                "Чат не зарегистрирован. Сначала напиши /start.",
            [ScrapperErrorCodes.InvalidLink] =
                "Это некорректная ссылка.\n" +
                "Пришли абсолютную ссылку с http/https, например:\n" +
                "https://github.com/dotnet/runtime",
            [ScrapperErrorCodes.InvalidLinkScheme] =
                "Это некорректная ссылка.\n" +
                "Пришли абсолютную ссылку с http/https, например:\n" +
                "https://github.com/dotnet/runtime",
            [ScrapperErrorCodes.ScrapperServiceUnavailable] =
                "Scrapper сейчас недоступен. Попробуй ещё раз чуть позже."
        };
}