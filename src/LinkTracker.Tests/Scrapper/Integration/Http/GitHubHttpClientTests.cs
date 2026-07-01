using System.Net;
using System.Text.Json;
using LinkTracker.Scrapper.Infrastructure.Clients.GitHub;
using LinkTracker.Scrapper.Infrastructure.Telemetry;

namespace LinkTracker.Tests.Scrapper.Integration.Http;

[Trait("Module", "Scrapper")]
[Trait("Category", "Integration")]
public sealed class GitHubHttpClientTests(WireMockContainerFixture wireMock) : IClassFixture<WireMockContainerFixture>
{
    [Fact]
    public async Task GetRepository_WhenResponseIsValid_ReturnsDeserializedBody()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", url = "/repos/user/repo" },
            response = new
            {
                status = 200,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                body = """
                       {
                         "id": 1,
                         "name": "repo",
                         "full_name": "user/repo",
                         "html_url": "https://github.com/user/repo",
                         "updated_at": "2025-03-07T12:00:00+00:00"
                       }
                       """
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new GitHubHttpClient(httpClient, metrics);

        var result = await sut.GetRepositoryAsync("user", "repo");

        Assert.Equal("user/repo", result.FullName);
        Assert.Equal(new Uri("https://github.com/user/repo"), result.HtmlUrl);
    }

    [Fact]
    public async Task GetRepository_WhenStatusIsNotSuccess_ThrowsHttpRequestException()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", url = "/repos/user/repo" },
            response = new
            {
                status = (int)HttpStatusCode.BadGateway,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                body = """
                       {
                         "message": "upstream failed"
                       }
                       """
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new GitHubHttpClient(httpClient, metrics);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetRepositoryAsync("user", "repo"));
    }

    [Fact]
    public async Task GetRepository_WhenBodyIsNull_ThrowsInvalidOperationException()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new { request = new { method = "GET", url = "/repos/user/repo" }, response = new { status = 200, headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }, body = "null" } });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new GitHubHttpClient(httpClient, metrics);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetRepositoryAsync("user", "repo"));
    }

    [Fact]
    public async Task GetRepository_WhenBodyDoesNotMatchSchema_ThrowsJsonException()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", url = "/repos/user/repo" },
            response = new
            {
                status = 200,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                body = """
                       {
                         "id": "oops",
                         "name": 123,
                         "full_name": 42,
                         "html_url": true,
                         "updated_at": {}
                       }
                       """
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new GitHubHttpClient(httpClient, metrics);

        await Assert.ThrowsAsync<JsonException>(() => sut.GetRepositoryAsync("user", "repo"));
    }

    [Fact]
    public async Task GetIssues_WhenResponseIsValid_ReturnsDeserializedBody()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", url = "/repos/user/repo/issues" },
            response = new
            {
                status = 200,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                body = """
                       [
                         {
                           "title": "Issue one",
                           "body": "Issue body text",
                           "created_at": "2025-03-07T12:00:00+00:00",
                           "html_url": "https://github.com/user/repo/issues/1",
                           "user": { "login": "octocat" }
                         }
                       ]
                       """
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new GitHubHttpClient(httpClient, metrics);

        var result = await sut.GetIssuesAsync("user", "repo");

        var issue = Assert.Single(result);
        Assert.Equal("Issue one", issue.Title);
        Assert.Equal("octocat", issue.User?.Login);
    }

    [Fact]
    public async Task GetIssues_WhenStatusIsNotSuccess_ThrowsHttpRequestException()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", url = "/repos/user/repo/issues" },
            response = new
            {
                status = (int)HttpStatusCode.BadGateway,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                body = """
                       {
                         "message": "upstream failed"
                       }
                       """
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new GitHubHttpClient(httpClient, metrics);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetIssuesAsync("user", "repo"));
    }

    [Fact]
    public async Task GetPullRequests_WhenResponseIsValid_ReturnsDeserializedBody()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", url = "/repos/user/repo/pulls" },
            response = new
            {
                status = 200,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                body = """
                       [
                         {
                           "title": "Add batching",
                           "body": "This PR adds batching support",
                           "created_at": "2025-03-08T10:00:00+00:00",
                           "html_url": "https://github.com/user/repo/pull/7",
                           "user": { "login": "octocat" }
                         }
                       ]
                       """
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new GitHubHttpClient(httpClient, metrics);

        var result = await sut.GetPullRequestsAsync("user", "repo");

        var pr = Assert.Single(result);
        Assert.Equal("Add batching", pr.Title);
        Assert.Equal("octocat", pr.User?.Login);
    }

    [Fact]
    public async Task GetPullRequests_WhenStatusIsNotSuccess_ThrowsHttpRequestException()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", url = "/repos/user/repo/pulls" },
            response = new
            {
                status = (int)HttpStatusCode.BadGateway,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                body = """
                       {
                         "message": "upstream failed"
                       }
                       """
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new GitHubHttpClient(httpClient, metrics);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetPullRequestsAsync("user", "repo"));
    }
}