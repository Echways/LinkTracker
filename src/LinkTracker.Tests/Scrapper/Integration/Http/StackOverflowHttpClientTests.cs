using System.Net;
using LinkTracker.Scrapper.Infrastructure.Clients.StackOverflow;
using LinkTracker.Scrapper.Infrastructure.Telemetry;

namespace LinkTracker.Tests.Scrapper.Integration.Http;

[Trait("Module", "Scrapper")]
[Trait("Category", "Integration")]
public sealed class StackOverflowHttpClientTests(WireMockContainerFixture wireMock) : IClassFixture<WireMockContainerFixture>
{
    [Fact]
    public async Task GetQuestion_WhenResponseIsValid_ReturnsFirstQuestion()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", url = "/2.3/questions/123?site=stackoverflow" },
            response = new
            {
                status = 200,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                jsonBody = new { items = new object[] { new { question_id = 123L, title = "How to test this?", link = "https://stackoverflow.com/questions/123/hello", last_activity_date = 1741348800L } } }
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new StackOverflowHttpClient(httpClient, metrics);

        var result = await sut.GetQuestionAsync(123);

        Assert.NotNull(result);
        Assert.Equal(123L, result!.QuestionId);
        Assert.Equal("How to test this?", result.Title);
    }

    [Fact]
    public async Task GetQuestion_WhenStatusIsNotSuccess_ThrowsHttpRequestException()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", url = "/2.3/questions/123?site=stackoverflow" },
            response = new { status = (int)HttpStatusCode.BadGateway, headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }, jsonBody = new { error_message = "upstream failed" } }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new StackOverflowHttpClient(httpClient, metrics);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetQuestionAsync(123));
    }

    [Fact]
    public async Task GetQuestion_WhenItemsIsEmpty_ReturnsNull()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new { request = new { method = "GET", url = "/2.3/questions/123?site=stackoverflow" }, response = new { status = 200, headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }, body = "{\"items\":[]}" } });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new StackOverflowHttpClient(httpClient, metrics);

        var result = await sut.GetQuestionAsync(123);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAnswers_WhenResponseIsValid_ReturnsAnswers()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", urlPath = "/2.3/questions/123/answers" },
            response = new
            {
                status = 200,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                jsonBody = new
                {
                    items = new object[]
                    {
                        new
                        {
                            answer_id = 777L,
                            body = "<p>Answer body</p>",
                            link = "https://stackoverflow.com/a/777",
                            creation_date = 1741348800L,
                            owner = new { display_name = "alice" }
                        }
                    }
                }
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new StackOverflowHttpClient(httpClient, metrics);

        var result = await sut.GetAnswersAsync(123);

        var answer = Assert.Single(result);
        Assert.Equal(777L, answer.AnswerId);
        Assert.Equal("<p>Answer body</p>", answer.Body);
        Assert.Equal("alice", answer.Owner!.DisplayName);
    }

    [Fact]
    public async Task GetComments_WhenResponseIsValid_ReturnsComments()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", urlPath = "/2.3/questions/123/comments" },
            response = new
            {
                status = 200,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                jsonBody = new
                {
                    items = new object[]
                    {
                        new
                        {
                            comment_id = 888L,
                            body = "<p>Comment body</p>",
                            link = "https://stackoverflow.com/questions/123/hello#comment888_123",
                            creation_date = 1741349800L,
                            owner = new { display_name = "bob" }
                        }
                    }
                }
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new StackOverflowHttpClient(httpClient, metrics);

        var result = await sut.GetCommentsAsync(123);

        var comment = Assert.Single(result);
        Assert.Equal(888L, comment.CommentId);
        Assert.Equal("<p>Comment body</p>", comment.Body);
        Assert.Equal("bob", comment.Owner!.DisplayName);
    }
}