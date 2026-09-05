using System.Net;
using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Shared.Contracts.Common;

namespace LinkTracker.Tests.Bot.Unit.Infrastructure.Clients.Scrapper;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class ScrapperErrorMessageMapperTests
{
    [Fact]
    public void TryMap_WhenUnsupportedLink_ReturnsHelpfulMessage()
    {
        var ex = CreateException(
            HttpStatusCode.BadRequest,
            ScrapperErrorCodes.UnsupportedLink,
            "Unsupported link");

        var result = ScrapperErrorMessageMapper.TryMap(ex, out var message);

        Assert.True(result);
        Assert.Contains("GitHub", message);
        Assert.Contains("StackOverflow", message);
        Assert.Contains("Reddit", message);
    }

    [Fact]
    public void TryMap_WhenUnknownError_ReturnsFalse()
    {
        var ex = CreateException(
            HttpStatusCode.BadRequest,
            "some_unknown_error",
            "Unknown");

        var result = ScrapperErrorMessageMapper.TryMap(ex, out var message);

        Assert.False(result);
        Assert.Equal(string.Empty, message);
    }

    [Theory]
    [InlineData(ScrapperErrorCodes.InvalidLink)]
    [InlineData(ScrapperErrorCodes.InvalidLinkScheme)]
    public void TryMap_WhenInvalidLink_ReturnsValidationMessage(string code)
    {
        var ex = CreateException(
            HttpStatusCode.BadRequest,
            code,
            "Invalid link");

        var result = ScrapperErrorMessageMapper.TryMap(ex, out var message);

        Assert.True(result);
        Assert.Contains("некорректная ссылка", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryMap_WhenScrapperServiceUnavailable_ReturnsFriendlyMessage()
    {
        var ex = new ScrapperClientException(
            HttpStatusCode.ServiceUnavailable,
            "Scrapper is unavailable")
        { FallbackCode = ScrapperErrorCodes.ScrapperServiceUnavailable };

        var result = ScrapperErrorMessageMapper.TryMap(ex, out var message);

        Assert.True(result);
        Assert.Contains("Scrapper", message, StringComparison.OrdinalIgnoreCase);
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
}