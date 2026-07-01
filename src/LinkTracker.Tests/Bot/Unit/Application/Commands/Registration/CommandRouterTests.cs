using LinkTracker.Bot.Application.Commands;
using LinkTracker.Bot.Application.Commands.Registration;
using LinkTracker.Bot.Application.Models;
using LinkTracker.Bot.Application.Telemetry.Abstractions;
using NSubstitute;

namespace LinkTracker.Tests.Bot.Unit.Application.Commands.Registration;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class CommandRouterTests
{
    [Fact]
    public async Task Route_WhenCommandMatches_ExecutesThatCommand()
    {
        var chatId = 1;

        var cmd = Substitute.For<ICommandHandler>();
        cmd.CanHandle("/start").Returns(true);
        cmd.ExecuteAsync(chatId, "/start", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OutgoingMessage(chatId, "ok")));

        var other = Substitute.For<ICommandHandler>();
        other.CanHandle(Arg.Any<string>()).Returns(false);

        var metrics = Substitute.For<IBotMetrics>();

        var sut = new CommandRouter([other, cmd], metrics);

        var result = await sut.RouteAsync(chatId, "/start");

        Assert.Equal(chatId, result.ChatId);
        Assert.Equal("ok", result.Text);
        await cmd.Received(1).ExecuteAsync(chatId, "/start", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Route_WhenNoCommandMatches_ReturnsDefaultHelpText()
    {
        var chatId = 2;

        var cmd = Substitute.For<ICommandHandler>();
        cmd.CanHandle(Arg.Any<string>()).Returns(false);

        var metrics = Substitute.For<IBotMetrics>();

        var sut = new CommandRouter([cmd], metrics);

        var result = await sut.RouteAsync(chatId, "/unknown");

        Assert.Equal(chatId, result.ChatId);
        Assert.Contains("/help", result.Text);
        await cmd.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default!);
    }

    [Fact]
    public async Task Route_PicksFirstMatchingCommand()
    {
        var chatId = 3;

        var first = Substitute.For<ICommandHandler>();
        first.CanHandle("/start").Returns(true);
        first.ExecuteAsync(chatId, "/start", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OutgoingMessage(chatId, "first")));

        var second = Substitute.For<ICommandHandler>();
        second.CanHandle("/start").Returns(true);
        second.ExecuteAsync(chatId, "/start", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OutgoingMessage(chatId, "second")));

        var metrics = Substitute.For<IBotMetrics>();

        var sut = new CommandRouter([first, second], metrics);

        var result = await sut.RouteAsync(chatId, "/start");

        Assert.Equal("first", result.Text);
        await first.Received(1).ExecuteAsync(chatId, "/start", Arg.Any<CancellationToken>());
        await second.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default!);
    }
}