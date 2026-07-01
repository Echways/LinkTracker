using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Implementations.Track.Nodes;

namespace LinkTracker.Tests.Bot.Unit.Application.Dialogs.Implementations.Track.Nodes;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class AskUrlNodeTests
{
    [Theory]
    [InlineData("https://github.com/user/repo")]
    [InlineData("http://example.com/path")]
    public async Task Handle_WhenUrlIsValid_SavesPendingUrlAndMovesToAskTags(string url)
    {
        var sut = new AskUrlNode();
        var ctx = new DialogContext { ChatId = 123L };
        var request = new BotRequest(
            123L,
            BotRequestType.Text,
            url);

        var result = await sut.HandleAsync(ctx, request, CancellationToken.None);

        Assert.False(result.EndDialog);
        Assert.Equal(AskTagsNode.NodeId, result.NextNodeId);
        Assert.Equal(url, ctx.Get("pending_url"));
        Assert.Contains("Добавить теги?", result.ReplyText);
    }

    [Fact]
    public async Task Handle_WhenTextIsEmpty_ReturnsTextPrompt()
    {
        var sut = new AskUrlNode();
        var ctx = new DialogContext { ChatId = 123L };
        var request = new BotRequest(
            123L,
            BotRequestType.Text,
            "");

        var result = await sut.HandleAsync(ctx, request, CancellationToken.None);

        Assert.False(result.EndDialog);
        Assert.Null(result.NextNodeId);
        Assert.Equal("Пришли URL текстом (https://...).", result.ReplyText);
        Assert.Null(ctx.Get("pending_url"));
    }

    [Fact]
    public async Task Handle_WhenTextIsNotAbsoluteUrl_ReturnsValidationMessage()
    {
        var sut = new AskUrlNode();
        var ctx = new DialogContext { ChatId = 123L };
        var request = new BotRequest(
            123L,
            BotRequestType.Text,
            "not-a-link");

        var result = await sut.HandleAsync(ctx, request, CancellationToken.None);

        Assert.False(result.EndDialog);
        Assert.Null(result.NextNodeId);
        Assert.Equal("Это не похоже на ссылку. Пришли URL целиком (https://...).", result.ReplyText);
        Assert.Null(ctx.Get("pending_url"));
    }

    [Fact]
    public async Task Handle_WhenAbsoluteUriWithCustomScheme_ReturnsValidationMessage()
    {
        var sut = new AskUrlNode();
        var ctx = new DialogContext { ChatId = 123L };
        var request = new BotRequest(
            123L,
            BotRequestType.Text,
            "tg://resolve?domain=test");

        var result = await sut.HandleAsync(ctx, request, CancellationToken.None);

        Assert.False(result.EndDialog);
        Assert.Null(result.NextNodeId);
        Assert.Equal("Это не похоже на ссылку. Пришли URL целиком (https://...).", result.ReplyText);
        Assert.Null(ctx.Get("pending_url"));
    }
}