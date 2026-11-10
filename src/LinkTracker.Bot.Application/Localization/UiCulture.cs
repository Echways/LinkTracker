using System.Globalization;

namespace LinkTracker.Bot.Application.Localization;

public static class UiCulture
{
    public static IDisposable Use(BotLanguage language)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = language.Culture;

        return new Restore(previous);
    }

    private sealed class Restore(CultureInfo previous) : IDisposable
    {
        public void Dispose()
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
