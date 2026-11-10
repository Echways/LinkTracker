using System.Globalization;

namespace LinkTracker.Bot.Application.Localization;

public sealed record BotLanguage(string Code, string DisplayName)
{
    public static readonly BotLanguage Russian = new("ru", "Русский");
    public static readonly BotLanguage English = new("en", "English");

    public static BotLanguage Default => English;

    public static IReadOnlyList<BotLanguage> All { get; } = [English, Russian];

    public CultureInfo Culture => CultureInfo.GetCultureInfo(Code);

    public static bool TryFind(string? code, out BotLanguage language)
    {
        var found = All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
        language = found ?? Default;

        return found is not null;
    }
}
