using LinkTracker.Bot.Application.Localization;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Clients.Scrapper;

public static class ScrapperErrorMessageMapper
{
    public static bool TryMap(ScrapperClientException ex, IStringLocalizer<Messages> localizer, out string message)
    {
        foreach (var (code, key) in ScrapperErrorMessages.KeyByCode)
        {
            if (!ex.HasCode(code))
            {
                continue;
            }

            message = localizer[key];
            return true;
        }

        message = string.Empty;
        return false;
    }
}
