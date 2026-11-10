using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Infrastructure.Storage.InMemory;

namespace LinkTracker.Tests.Bot.Unit.Application.Localization;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class ChatLanguageServiceTests
{
    [Fact]
    public async Task GetAsync_WhenNothingStored_ReturnsDefault()
    {
        var sut = new ChatLanguageService(new InMemoryLanguageStore());

        var language = await sut.GetAsync(1, CancellationToken.None);

        Assert.Equal(BotLanguage.English, language);
    }

    [Fact]
    public async Task GetAsync_AfterSet_ReturnsChosenLanguagePerChat()
    {
        var sut = new ChatLanguageService(new InMemoryLanguageStore());

        await sut.SetAsync(1, BotLanguage.Russian, CancellationToken.None);

        Assert.Equal(BotLanguage.Russian, await sut.GetAsync(1, CancellationToken.None));
        Assert.Equal(BotLanguage.English, await sut.GetAsync(2, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_WhenStoredCodeIsUnknown_ReturnsDefault()
    {
        var store = new InMemoryLanguageStore();
        await store.SetAsync(1, "xx", CancellationToken.None);
        var sut = new ChatLanguageService(store);

        Assert.Equal(BotLanguage.English, await sut.GetAsync(1, CancellationToken.None));
    }

    [Theory]
    [InlineData("ru", true)]
    [InlineData("RU", true)]
    [InlineData("en", true)]
    [InlineData("de", false)]
    [InlineData(null, false)]
    public void TryFind_RecognisesOnlySupportedCodes(string? code, bool expected)
    {
        Assert.Equal(expected, BotLanguage.TryFind(code, out _));
    }
}
