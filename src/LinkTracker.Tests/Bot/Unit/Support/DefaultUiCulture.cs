using System.Globalization;
using System.Runtime.CompilerServices;
using LinkTracker.Bot.Application.Localization;

namespace LinkTracker.Tests.Bot.Unit.Support;

internal static class DefaultUiCulture
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        CultureInfo.DefaultThreadCurrentUICulture = BotLanguage.Default.Culture;
        CultureInfo.CurrentUICulture = BotLanguage.Default.Culture;
    }
}
