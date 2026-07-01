using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Runtime;
using NSubstitute;

namespace LinkTracker.Tests.Bot.Unit.Application.Dialogs.Runtime;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class DialogManagerTests
{
    [Fact]
    public async Task TryHandle_WhenDialogContinues_SavesUpdatedContextToStore()
    {
        var store = Substitute.For<IDialogStateStore>();
        var ctx = new DialogContext { ChatId = 123L, ActiveDialogId = "track", ActiveNodeId = "ask_url" };

        store.GetOrCreateAsync(123L, Arg.Any<CancellationToken>())
            .Returns(ctx);

        var node = Substitute.For<IDialogNode>();
        node.HandleAsync(Arg.Any<DialogContext>(), Arg.Any<BotRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var dialogContext = callInfo.Arg<DialogContext>();
                dialogContext.Set("pending_url", "https://github.com/user/repo");

                return new DialogNodeResult(
                    "ok",
                    "ask_tags");
            });

        var dialog = Substitute.For<IDialog>();
        dialog.Id.Returns("track");
        dialog.StartNodeId.Returns("ask_url");
        dialog.Nodes.Returns(new Dictionary<string, IDialogNode> { ["ask_url"] = node });

        var sut = new DialogManager(store, [dialog]);

        var result = await sut.TryHandleAsync(
            new BotRequest(123L, BotRequestType.Text, "https://github.com/user/repo"),
            CancellationToken.None);

        Assert.True(result.handled);
        Assert.Equal("ok", result.replyText);
        Assert.Equal("ask_tags", ctx.ActiveNodeId);
        Assert.Equal("https://github.com/user/repo", ctx.Get("pending_url"));

        await store.Received(1).SaveAsync(
            Arg.Is<DialogContext>(x =>
                x.ChatId == 123L &&
                x.ActiveDialogId == "track" &&
                x.ActiveNodeId == "ask_tags" &&
                x.Get("pending_url") == "https://github.com/user/repo"),
            Arg.Any<CancellationToken>());
    }
}