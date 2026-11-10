using LinkTracker.Tests.Bot.Unit.Support;
using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Clients.Scrapper.Contracts.Responses;
using LinkTracker.Bot.Application.Commands.Implementations;
using NSubstitute;

namespace LinkTracker.Tests.Bot.Unit.Application.Commands.Implementations;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class ListCommandTests
{
    [Fact]
    public async Task Execute_WhenNoLinks_ReturnsEmptyMessage()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        scrapperClient.GetLinksAsync(123L, Arg.Any<CancellationToken>())
            .Returns(new ListLinksResponse { Links = [], Size = 0 });

        var sut = new ListCommand(scrapperClient, TestLocalization.Localizer);

        var result = await sut.ExecuteAsync(123L, "/list");

        Assert.Equal(123L, result.ChatId);
        Assert.Equal("No tracked links yet. Add one with /track", result.Text);
    }

    [Fact]
    public async Task Execute_WhenTagProvided_FiltersIgnoringCase()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        scrapperClient.GetLinksAsync(123L, Arg.Any<CancellationToken>())
            .Returns(new ListLinksResponse
            {
                Links =
                [
                    new LinkResponse { Id = 1, Url = new Uri("https://github.com/dotnet/runtime"), Tags = ["dotnet", "backend"] },
                    new LinkResponse { Id = 2, Url = new Uri("https://stackoverflow.com/questions/123/test"), Tags = ["frontend"] }
                ],
                Size = 2
            });

        var sut = new ListCommand(scrapperClient, TestLocalization.Localizer);

        var result = await sut.ExecuteAsync(123L, "/list BACKEND");

        Assert.Equal(123L, result.ChatId);
        Assert.Contains("https://github.com/dotnet/runtime", result.Text);
        Assert.DoesNotContain("https://stackoverflow.com/questions/123/test", result.Text);
        Assert.Contains("tag: BACKEND", result.Text);
    }

    [Fact]
    public async Task Execute_WhenTagProvidedAndNothingFound_ReturnsFriendlyMessage()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        scrapperClient.GetLinksAsync(123L, Arg.Any<CancellationToken>())
            .Returns(new ListLinksResponse
            {
                Links =
                [
                    new LinkResponse { Id = 1, Url = new Uri("https://github.com/dotnet/runtime"), Tags = ["dotnet"] }
                ],
                Size = 1
            });

        var sut = new ListCommand(scrapperClient, TestLocalization.Localizer);

        var result = await sut.ExecuteAsync(123L, "/list java");

        Assert.Equal(123L, result.ChatId);
        Assert.Equal("No tracked links found with the tag “java”.", result.Text);
    }

    [Fact]
    public async Task Execute_WhenLinksExist_ReturnsTrackedLinksList()
    {
        var scrapperClient = Substitute.For<IScrapperClient>();
        scrapperClient.GetLinksAsync(123L, Arg.Any<CancellationToken>())
            .Returns(new ListLinksResponse
            {
                Links =
                [
                    new LinkResponse { Id = 1, Url = new Uri("https://github.com/dotnet/runtime"), Tags = ["dotnet", "backend"] },
                    new LinkResponse { Id = 2, Url = new Uri("https://stackoverflow.com/questions/123/test"), Tags = [] }
                ],
                Size = 2
            });

        var sut = new ListCommand(scrapperClient, TestLocalization.Localizer);

        var result = await sut.ExecuteAsync(123L, "/list");

        Assert.Equal(123L, result.ChatId);
        Assert.Contains("Tracked links:", result.Text);
        Assert.Contains("https://github.com/dotnet/runtime", result.Text);
        Assert.Contains("https://stackoverflow.com/questions/123/test", result.Text);
        Assert.Contains("Tags: dotnet, backend", result.Text);
        Assert.Contains("Tags: —", result.Text);
    }
}