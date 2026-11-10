using System.Globalization;
using System.Reflection;
using System.Resources;
using LinkTracker.Bot.Application.Localization;

namespace LinkTracker.Tests.Bot.Unit.Application.Localization;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class MessagesCatalogTests
{
    private static readonly ResourceManager Manager = new(typeof(Messages));

    public static TheoryData<string> LanguageCodes()
    {
        return new TheoryData<string>(BotLanguage.All.Select(l => l.Code));
    }

    [Theory]
    [MemberData(nameof(LanguageCodes))]
    public void Catalog_ContainsExactlyTheDeclaredKeys(string code)
    {
        var declared = typeof(Texts)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (string)f.GetRawConstantValue()!)
            .Order()
            .ToList();

        var set = Manager.GetResourceSet(CultureInfo.GetCultureInfo(code), true, true)!;
        var actual = set.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).Order().ToList();

        Assert.Equal(declared, actual);
    }

    [Theory]
    [MemberData(nameof(LanguageCodes))]
    public void Catalog_HasNoEmptyValues(string code)
    {
        var set = Manager.GetResourceSet(CultureInfo.GetCultureInfo(code), true, true)!;

        Assert.All(
            set.Cast<System.Collections.DictionaryEntry>(),
            e => Assert.False(string.IsNullOrWhiteSpace((string?)e.Value), (string)e.Key));
    }

    [Fact]
    public void Catalog_RussianDiffersFromEnglish()
    {
        var english = Manager.GetString(Texts.HelpHeader, CultureInfo.GetCultureInfo("en"));
        var russian = Manager.GetString(Texts.HelpHeader, CultureInfo.GetCultureInfo("ru"));

        Assert.Equal("Available commands:", english);
        Assert.Equal("Доступные команды:", russian);
    }
}
