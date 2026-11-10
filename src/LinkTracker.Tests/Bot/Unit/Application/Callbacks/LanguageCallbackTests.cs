using System.Globalization;
using LinkTracker.Bot.Application.Callbacks;
using LinkTracker.Bot.Application.Commands.Implementations;
using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Infrastructure.Storage.InMemory;
using LinkTracker.Tests.Bot.Unit.Support;
using NSubstitute;

namespace LinkTracker.Tests.Bot.Unit.Application.Callbacks;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class LanguageCallbackTests
{
    private const long ChatId = 7;

    [Fact]
    public async Task Command_OffersEveryLanguageAndMarksTheCurrentOne()
    {
        var languages = TestLocalization.CreateLanguages();
        await languages.SetAsync(ChatId, BotLanguage.Russian, CancellationToken.None);
        var sut = new LanguageCommand(languages, TestLocalization.Localizer);

        var result = await sut.ExecuteAsync(ChatId, "/language");

        Assert.NotNull(result.Buttons);
        Assert.Equal(["English", "✅ Русский"], result.Buttons.Select(b => b.Text));
        Assert.Equal(["lang:en", "lang:ru"], result.Buttons.Select(b => b.CallbackData));
    }

    [Fact]
    public async Task Handler_SavesLanguageUpdatesMenuAndEditsMessageInNewLanguage()
    {
        var store = new InMemoryLanguageStore();
        var menu = Substitute.For<IChatMenuUpdater>();
        var sut = new LanguageCallbackHandler(
            TestLocalization.CreateLanguages(store),
            menu,
            TestLocalization.Localizer);
        var request = new BotRequest(ChatId, BotRequestType.Callback, CallbackData: "lang:ru", MessageId: 55);

        var result = await sut.HandleAsync(request, CancellationToken.None);

        Assert.Equal("ru", await store.GetAsync(ChatId, CancellationToken.None));
        Assert.Equal("Язык переключён: Русский", result.Text);
        Assert.Equal(55, result.EditMessageId);
        Assert.Null(result.Buttons);
        await menu.Received(1).UpdateAsync(ChatId, BotLanguage.Russian, Arg.Any<CancellationToken>());
        Assert.Equal("en", CultureInfo.CurrentUICulture.Name);
    }

    [Theory]
    [InlineData("lang:ru", true)]
    [InlineData("lang:en", true)]
    [InlineData("lang:de", false)]
    [InlineData("other:ru", false)]
    public void Handler_CanHandleOnlyKnownLanguageCallbacks(string data, bool expected)
    {
        var sut = new LanguageCallbackHandler(
            TestLocalization.CreateLanguages(),
            Substitute.For<IChatMenuUpdater>(),
            TestLocalization.Localizer);

        Assert.Equal(expected, sut.CanHandle(data));
    }

    [Fact]
    public async Task Router_WhenNoHandlerMatches_ReturnsNotUnderstood()
    {
        var sut = new CallbackRouter([], TestLocalization.Localizer);

        var result = await sut.RouteAsync(
            new BotRequest(ChatId, BotRequestType.Callback, CallbackData: "unknown"),
            CancellationToken.None);

        Assert.Equal("I did not get that. Type /help", result.Text);
    }
}
