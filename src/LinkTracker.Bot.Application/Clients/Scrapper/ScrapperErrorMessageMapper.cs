namespace LinkTracker.Bot.Application.Clients.Scrapper;

public static class ScrapperErrorMessageMapper
{
    public static bool TryMap(ScrapperClientException ex, out string message)
    {
        foreach (var (code, text) in ScrapperErrorMessages.ByCode)
        {
            if (!ex.HasCode(code))
            {
                continue;
            }

            message = text;
            return true;
        }

        message = string.Empty;
        return false;
    }
}