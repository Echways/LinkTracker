using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Infrastructure.Storage.InMemory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkTracker.Tests.Bot.Unit.Support;

internal static class TestLocalization
{
    public static IStringLocalizer<Messages> Localizer { get; } =
        new StringLocalizer<Messages>(
            new ResourceManagerStringLocalizerFactory(
                Options.Create(new LocalizationOptions()),
                NullLoggerFactory.Instance));

    public static ChatLanguageService CreateLanguages(InMemoryLanguageStore? store = null)
    {
        return new ChatLanguageService(store ?? new InMemoryLanguageStore());
    }
}
