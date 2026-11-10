using LinkTracker.Bot.Application.Localization;
using LinkTracker.Shared.Contracts.Common;

namespace LinkTracker.Bot.Application.Clients.Scrapper;

internal static class ScrapperErrorMessages
{
    internal static readonly IReadOnlyDictionary<string, string> KeyByCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ScrapperErrorCodes.UnsupportedLink] = Texts.ErrorUnsupportedLink,
            [ScrapperErrorCodes.ChatAlreadyExists] = Texts.ErrorChatAlreadyExists,
            [ScrapperErrorCodes.LinkAlreadyExists] = Texts.ErrorLinkAlreadyExists,
            [ScrapperErrorCodes.LinkNotFound] = Texts.ErrorLinkNotFound,
            [ScrapperErrorCodes.ChatNotFound] = Texts.ErrorChatNotFound,
            [ScrapperErrorCodes.InvalidLink] = Texts.ErrorInvalidLink,
            [ScrapperErrorCodes.InvalidLinkScheme] = Texts.ErrorInvalidLink,
            [ScrapperErrorCodes.ScrapperServiceUnavailable] = Texts.ErrorScrapperUnavailable
        };
}
