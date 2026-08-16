using System.Net;
using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Clients.Scrapper.Contracts.Responses;
using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Implementations.Track.Nodes;
using LinkTracker.Shared.Contracts.Common;
using NSubstitute;

namespace LinkTracker.Tests.Bot.Unit.Application.Dialogs.Implementations.Track.Nodes;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class TrackConfirmNodeTests
{
    [Fact]
    public async Task Handle_WhenUserAnswersYesAndLinkAdded_ReturnsSuccessAndEndsDialog()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        var uri = new Uri("https://github.com/dotnet/runtime");

        scrapperClient
            .AddLinkAsync(
                123L,
                uri,
                Arg.Is<IReadOnlyList<string>>(x => x.SequenceEqual(new[] { "dotnet", "runtime" })),
                Arg.Any<CancellationToken>())
            .Returns(new LinkResponse { Id = 1, Url = uri, Tags = ["dotnet", "runtime"] });

        var sut = new TrackConfirmNode(scrapperClient);
        var ctx = CreateContext(123L, uri.ToString(), "dotnet, runtime");
        var request = new BotRequest(123L, BotRequestType.Text, "да");

        var result = await sut.HandleAsync(ctx, request, CancellationToken.None);

        Assert.True(result.EndDialog);
        Assert.Contains("Начал отслеживать", result.ReplyText);
        Assert.Contains(uri.ToString(), result.ReplyText);
        Assert.Contains("dotnet, runtime", result.ReplyText);

        await scrapperClient.Received(1).AddLinkAsync(
            123L,
            uri,
            Arg.Is<IReadOnlyList<string>>(x => x.SequenceEqual(new[] { "dotnet", "runtime" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserAnswersNo_ClearsTrackStateAndReturnsAskUrlNode()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        var sut = new TrackConfirmNode(scrapperClient);
        var ctx = CreateContext(123L, "https://github.com/dotnet/runtime", "dotnet");
        var request = new BotRequest(123L, BotRequestType.Text, "нет");

        var result = await sut.HandleAsync(ctx, request, CancellationToken.None);

        Assert.False(result.EndDialog);
        Assert.Equal(AskUrlNode.NodeId, result.NextNodeId);
        Assert.Equal("Ок.\nПришли другую ссылку.", result.ReplyText);

        Assert.Null(ctx.Get("pending_url"));
        Assert.Null(ctx.Get("tags_csv"));

        await scrapperClient.DidNotReceive()
            .AddLinkAsync(
                Arg.Any<long>(),
                Arg.Any<Uri>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUnsupportedLink_ReturnsRetryMessage()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();

        scrapperClient
            .AddLinkAsync(
                123L,
                Arg.Any<Uri>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<LinkResponse>(
                CreateException(
                    HttpStatusCode.BadRequest,
                    ScrapperErrorCodes.UnsupportedLink,
                    "Unsupported link")));

        var sut = new TrackConfirmNode(scrapperClient);
        var ctx = CreateContext(123L, "https://example.com/test", "");
        var request = new BotRequest(123L, BotRequestType.Text, "да");

        var result = await sut.HandleAsync(ctx, request, CancellationToken.None);

        Assert.False(result.EndDialog);
        Assert.Equal(AskUrlNode.NodeId, result.NextNodeId);
        Assert.Contains("не поддерживается", result.ReplyText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Пришли другую ссылку.", result.ReplyText);
    }

    [Fact]
    public async Task Handle_WhenLinkAlreadyExists_ReturnsFriendlyMessageAndEndsDialog()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();

        scrapperClient
            .AddLinkAsync(
                123L,
                Arg.Any<Uri>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<LinkResponse>(
                CreateException(
                    HttpStatusCode.Conflict,
                    ScrapperErrorCodes.LinkAlreadyExists,
                    "Already exists")));

        var sut = new TrackConfirmNode(scrapperClient);
        var ctx = CreateContext(123L, "https://github.com/dotnet/runtime", "");
        var request = new BotRequest(123L, BotRequestType.Text, "да");

        var result = await sut.HandleAsync(ctx, request, CancellationToken.None);

        Assert.True(result.EndDialog);
        Assert.Contains("уже отслеживается", result.ReplyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_WhenUnknownScrapperError_ReturnsFallbackMessage()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();

        scrapperClient
            .AddLinkAsync(
                123L,
                Arg.Any<Uri>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<LinkResponse>(
                new ScrapperClientException(
                    HttpStatusCode.BadGateway,
                    "Gateway error")));

        var sut = new TrackConfirmNode(scrapperClient);
        var ctx = CreateContext(123L, "https://github.com/dotnet/runtime", "");
        var request = new BotRequest(123L, BotRequestType.Text, "да");

        var result = await sut.HandleAsync(ctx, request, CancellationToken.None);

        Assert.True(result.EndDialog);
        Assert.Contains("ошибки scrapper", result.ReplyText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("может быть")]
    [InlineData("")]
    public async Task Handle_WhenAnswerIsNotYesOrNo_ReturnsPrompt(string text)
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        var sut = new TrackConfirmNode(scrapperClient);
        var ctx = CreateContext(123L, "https://github.com/dotnet/runtime", "");
        var request = new BotRequest(123L, BotRequestType.Text, text);

        var result = await sut.HandleAsync(ctx, request, CancellationToken.None);

        Assert.False(result.EndDialog);
        Assert.Equal("'да' / 'нет'.", result.ReplyText);
    }

    private static ScrapperClientException CreateException(
        HttpStatusCode statusCode,
        string code,
        string description)
    {
        return new ScrapperClientException(
            statusCode,
            description,
            new ApiErrorResponse { Code = code, Description = description });
    }

    private static DialogContext CreateContext(long chatId, string pendingUrl, string tagsCsv)
    {
        var ctx = new DialogContext { ChatId = chatId };
        ctx.Set("pending_url", pendingUrl);
        ctx.Set("tags_csv", tagsCsv);
        return ctx;
    }
}