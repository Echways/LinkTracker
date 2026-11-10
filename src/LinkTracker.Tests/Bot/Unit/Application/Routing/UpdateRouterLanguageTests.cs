using LinkTracker.Bot.Application.Callbacks;
using LinkTracker.Bot.Application.Commands.Registration;
using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Runtime;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Application.Routing;
using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Bot.Infrastructure.Storage.InMemory;
using LinkTracker.Tests.Bot.Unit.Support;
using NSubstitute;

namespace LinkTracker.Tests.Bot.Unit.Application.Routing;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class UpdateRouterLanguageTests
{
    private const long ChatId = 5;

    [Fact]
    public async Task Route_RepliesInTheLanguageChosenForTheChat()
    {
        var languages = TestLocalization.CreateLanguages();
        await languages.SetAsync(ChatId, BotLanguage.Russian, CancellationToken.None);
        var sut = CreateRouter(languages, []);

        var result = await sut.RouteAsync(
            new BotRequest(ChatId, BotRequestType.Command, "/unknown", "unknown"),
            CancellationToken.None);

        Assert.Equal("Не понял. Напиши /help", result.Text);
    }

    [Fact]
    public async Task Route_RepliesInEnglishByDefault()
    {
        var sut = CreateRouter(TestLocalization.CreateLanguages(), []);

        var result = await sut.RouteAsync(
            new BotRequest(ChatId, BotRequestType.Command, "/unknown", "unknown"),
            CancellationToken.None);

        Assert.Equal("I did not get that. Type /help", result.Text);
    }

    [Fact]
    public async Task Route_WhenCallback_UsesCallbackHandlerAndChangesLanguage()
    {
        var store = new InMemoryLanguageStore();
        var languages = TestLocalization.CreateLanguages(store);
        var handler = new LanguageCallbackHandler(
            languages,
            Substitute.For<IChatMenuUpdater>(),
            TestLocalization.Localizer);
        var sut = CreateRouter(languages, [handler]);

        var result = await sut.RouteAsync(
            new BotRequest(ChatId, BotRequestType.Callback, CallbackData: "lang:ru", MessageId: 9),
            CancellationToken.None);

        Assert.Equal("ru", await store.GetAsync(ChatId, CancellationToken.None));
        Assert.Equal(9, result.EditMessageId);
    }

    private static UpdateRouter CreateRouter(ChatLanguageService languages, ICallbackHandler[] callbackHandlers)
    {
        var metrics = Substitute.For<IBotMetrics>();
        var dialogStore = Substitute.For<IDialogStateStore>();
        dialogStore.GetOrCreateAsync(ChatId, Arg.Any<CancellationToken>())
            .Returns(new DialogContext { ChatId = ChatId });

        return new UpdateRouter(
            new CommandRouter([], metrics, TestLocalization.Localizer),
            new CallbackRouter(callbackHandlers, TestLocalization.Localizer),
            new DialogManager(dialogStore, [], TestLocalization.Localizer),
            languages,
            TestLocalization.Localizer,
            metrics);
    }
}
