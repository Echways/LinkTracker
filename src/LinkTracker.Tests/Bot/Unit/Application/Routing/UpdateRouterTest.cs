using LinkTracker.Bot.Application.Commands;
using LinkTracker.Bot.Application.Commands.Registration;
using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Runtime;
using LinkTracker.Bot.Application.Models;
using LinkTracker.Bot.Application.Routing;
using LinkTracker.Bot.Application.Telemetry.Abstractions;
using NSubstitute;

namespace LinkTracker.Tests.Bot.Unit.Application.Routing;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class UpdateRouterTests
{
    private const string DialogInterruptedNotice = "Прерван незавершённый диалог, введённые данные не сохранены.";

    [Fact]
    public async Task Route_WhenCommandAndDialogActive_RoutesToCommandFirst()
    {
        var chatId = 123L;
        var request = new BotRequest(
            chatId,
            BotRequestType.Command,
            "/help",
            "help");

        var command = Substitute.For<ICommandHandler>();
        command.CanHandle("/help").Returns(true);
        command.ExecuteAsync(chatId, "/help", Arg.Any<CancellationToken>())
            .Returns(new OutgoingMessage(chatId, "command handled"));

        var store = Substitute.For<IDialogStateStore>();
        store.GetOrCreateAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new DialogContext { ChatId = chatId, ActiveDialogId = "track", ActiveNodeId = "confirm" });

        var dialogNode = Substitute.For<IDialogNode>();
        var dialog = Substitute.For<IDialog>();
        dialog.Id.Returns("track");
        dialog.StartNodeId.Returns("confirm");
        dialog.Nodes.Returns(new Dictionary<string, IDialogNode> { ["confirm"] = dialogNode });

        var metrics = Substitute.For<IBotMetrics>();

        var sut = new UpdateRouter(
            new CommandRouter([command], metrics),
            new DialogManager(store, [dialog]),
            metrics
        );

        var result = await sut.RouteAsync(request, CancellationToken.None);

        Assert.StartsWith(DialogInterruptedNotice, result.Text, StringComparison.Ordinal);
        Assert.EndsWith("command handled", result.Text, StringComparison.Ordinal);

        await command.Received(1).ExecuteAsync(chatId, "/help", Arg.Any<CancellationToken>());
        await dialogNode.DidNotReceive()
            .HandleAsync(Arg.Any<DialogContext>(), Arg.Any<BotRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Route_WhenDialogInactive_RoutesCommandUsingFallbackCommandText()
    {
        var chatId = 123L;
        var request = new BotRequest(
            chatId,
            BotRequestType.Command,
            null,
            "help");

        var command = Substitute.For<ICommandHandler>();
        command.CanHandle("/help").Returns(true);
        command.ExecuteAsync(chatId, "/help", Arg.Any<CancellationToken>())
            .Returns(new OutgoingMessage(chatId, "command handled"));

        var store = Substitute.For<IDialogStateStore>();
        store.GetOrCreateAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new DialogContext { ChatId = chatId });

        var metrics = Substitute.For<IBotMetrics>();

        var sut = new UpdateRouter(
            new CommandRouter([command], metrics),
            new DialogManager(store, Array.Empty<IDialog>()),
            metrics
        );

        var result = await sut.RouteAsync(request, CancellationToken.None);

        Assert.Equal("command handled", result.Text);
        await command.Received(1).ExecuteAsync(chatId, "/help", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Route_WhenCommandReceivedAndDialogActive_RoutesThroughCommandRouter()
    {
        var chatId = 123L;
        var request = new BotRequest(
            chatId,
            BotRequestType.Command,
            "/track",
            "track");

        var command = Substitute.For<ICommandHandler>();
        command.CanHandle("/track").Returns(true);
        command.ExecuteAsync(chatId, "/track", Arg.Any<CancellationToken>())
            .Returns(new OutgoingMessage(chatId, "command handled"));

        var store = Substitute.For<IDialogStateStore>();
        store.GetOrCreateAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new DialogContext { ChatId = chatId, ActiveDialogId = "track", ActiveNodeId = "confirm" });

        var dialogNode = Substitute.For<IDialogNode>();

        var dialog = Substitute.For<IDialog>();
        dialog.Id.Returns("track");
        dialog.StartNodeId.Returns("confirm");
        dialog.Nodes.Returns(new Dictionary<string, IDialogNode> { ["confirm"] = dialogNode });

        var metrics = Substitute.For<IBotMetrics>();

        var sut = new UpdateRouter(
            new CommandRouter([command], metrics),
            new DialogManager(store, [dialog]),
            metrics
        );

        var result = await sut.RouteAsync(request, CancellationToken.None);

        Assert.StartsWith(DialogInterruptedNotice, result.Text, StringComparison.Ordinal);
        Assert.EndsWith("command handled", result.Text, StringComparison.Ordinal);

        await command.Received(1)
            .ExecuteAsync(chatId, "/track", Arg.Any<CancellationToken>());

        await dialogNode.DidNotReceive()
            .HandleAsync(Arg.Any<DialogContext>(), Arg.Any<BotRequest>(), Arg.Any<CancellationToken>());
    }
}