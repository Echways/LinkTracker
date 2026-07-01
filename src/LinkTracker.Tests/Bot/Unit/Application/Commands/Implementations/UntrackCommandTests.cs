using System.Net;
using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Clients.Scrapper.Contracts.Responses;
using LinkTracker.Bot.Application.Commands.Implementations;
using LinkTracker.Shared.Contracts.Common;
using NSubstitute;

namespace LinkTracker.Tests.Bot.Unit.Application.Commands.Implementations;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class UntrackCommandTests
{
    [Fact]
    public async Task Execute_WhenArgsMissing_ReturnsUsageMessage()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        var sut = new UntrackCommand(scrapperClient);

        var result = await sut.ExecuteAsync(123L, "/untrack");

        Assert.Equal(123L, result.ChatId);
        Assert.Equal("Пришли команду в формате:\n/untrack <url>", result.Text);

        await scrapperClient.DidNotReceive()
            .RemoveLinkAsync(Arg.Any<long>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenUrlInvalid_ReturnsValidationMessage()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        var sut = new UntrackCommand(scrapperClient);

        var result = await sut.ExecuteAsync(123L, "/untrack not-a-url");

        Assert.Equal(123L, result.ChatId);
        Assert.Equal("Это не похоже на корректную ссылку.\nИспользуй формат:\n/untrack <url>", result.Text);

        await scrapperClient.DidNotReceive()
            .RemoveLinkAsync(Arg.Any<long>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenLinkRemoved_ReturnsSuccessMessage()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        var uri = new Uri("https://github.com/dotnet/runtime");

        scrapperClient.RemoveLinkAsync(123L, uri, Arg.Any<CancellationToken>())
            .Returns(new LinkResponse { Id = 1, Url = uri, Tags = ["dotnet"] });

        var sut = new UntrackCommand(scrapperClient);

        var result = await sut.ExecuteAsync(123L, $"/untrack {uri}");

        Assert.Equal(123L, result.ChatId);
        Assert.Equal($"Больше не отслеживаю:\n{uri}", result.Text);

        await scrapperClient.Received(1)
            .RemoveLinkAsync(123L, uri, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenScrapperReturnsMappedError_ReturnsFriendlyMessage()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        var uri = new Uri("https://github.com/dotnet/runtime");

        scrapperClient.RemoveLinkAsync(123L, uri, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<LinkResponse>(
                new ScrapperClientException(
                    HttpStatusCode.NotFound,
                    "Link not found",
                    new ApiErrorResponse { Code = ScrapperErrorCodes.LinkNotFound, Description = "Link not found" })));

        var sut = new UntrackCommand(scrapperClient);

        var result = await sut.ExecuteAsync(123L, $"/untrack {uri}");

        Assert.Equal(123L, result.ChatId);
        Assert.Equal("Эта ссылка не найдена в отслеживаемых.", result.Text);
    }
}