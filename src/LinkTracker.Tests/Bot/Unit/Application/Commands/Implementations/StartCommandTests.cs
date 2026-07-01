using System.Net;
using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Commands.Implementations;
using LinkTracker.Shared.Contracts.Common;
using NSubstitute;

namespace LinkTracker.Tests.Bot.Unit.Application.Commands.Implementations;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class StartCommandTests
{
    [Fact]
    public async Task Execute_WhenChatRegistered_ReturnsSuccessMessage()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        scrapperClient
            .RegisterChatAsync(123L, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var command = new StartCommand(scrapperClient);

        var result = await command.ExecuteAsync(123L, string.Empty, CancellationToken.None);

        Assert.Contains("Привет!", result.Text, StringComparison.OrdinalIgnoreCase);

        await scrapperClient.Received(1).RegisterChatAsync(123L, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenChatAlreadyExists_ReturnsFriendlyMessage()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        scrapperClient
            .RegisterChatAsync(123L, Arg.Any<CancellationToken>())
            .Returns(_ => throw new ScrapperClientException(
                HttpStatusCode.Conflict,
                "Chat already exists",
                new ApiErrorResponse { Code = ScrapperErrorCodes.ChatAlreadyExists, Description = "Chat already exists" }));

        var command = new StartCommand(scrapperClient);

        var result = await command.ExecuteAsync(123L, string.Empty, CancellationToken.None);

        Assert.Contains("Чат уже зарегистрирован", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_WhenScrapperServiceUnavailable_ReturnsMappedMessage()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        scrapperClient
            .RegisterChatAsync(123L, Arg.Any<CancellationToken>())
            .Returns(_ => throw new ScrapperClientException(
                HttpStatusCode.ServiceUnavailable,
                "Scrapper is unavailable")
            { FallbackCode = ScrapperErrorCodes.ScrapperServiceUnavailable });

        var command = new StartCommand(scrapperClient);

        var result = await command.ExecuteAsync(123L, string.Empty, CancellationToken.None);

        Assert.Contains("scrapper", result.Text, StringComparison.OrdinalIgnoreCase);
    }
}