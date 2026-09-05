using System.Net;
using LinkTracker.Scrapper.Infrastructure.Clients.Reddit;
using LinkTracker.Scrapper.Infrastructure.Telemetry;

namespace LinkTracker.Tests.Scrapper.Integration.Http;

[Trait("Module", "Scrapper")]
[Trait("Category", "Integration")]
public sealed class RedditHttpClientTests(WireMockContainerFixture wireMock) : IClassFixture<WireMockContainerFixture>
{
    [Fact]
    public async Task GetNewPosts_WhenResponseIsValid_ReturnsUnwrappedPosts()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", url = "/r/dotnet/new?limit=100" },
            response = new
            {
                status = 200,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                jsonBody = new
                {
                    kind = "Listing",
                    data = new
                    {
                        children = new object[]
                        {
                            new
                            {
                                kind = "t3",
                                data = new
                                {
                                    id = "abc123",
                                    title = "Announcing .NET 10",
                                    selftext = "Release notes inside",
                                    author = "alice",
                                    permalink = "/r/dotnet/comments/abc123/announcing_net_10/",
                                    created_utc = 1741348800.5
                                }
                            },
                            new
                            {
                                kind = "t3",
                                data = new
                                {
                                    id = "def456",
                                    title = "Weekly thread",
                                    selftext = "",
                                    author = "bob",
                                    permalink = "/r/dotnet/comments/def456/weekly_thread/",
                                    created_utc = 1741262400.0
                                }
                            }
                        }
                    }
                }
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new RedditHttpClient(httpClient, metrics);

        var result = await sut.GetNewPostsAsync("dotnet");

        Assert.Equal(2, result.Count);

        Assert.Equal("abc123", result[0].Id);
        Assert.Equal("Announcing .NET 10", result[0].Title);
        Assert.Equal("Release notes inside", result[0].Selftext);
        Assert.Equal("alice", result[0].Author);
        Assert.Equal("/r/dotnet/comments/abc123/announcing_net_10/", result[0].Permalink);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1741348800), result[0].CreatedAt);

        Assert.Equal("def456", result[1].Id);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1741262400), result[1].CreatedAt);
    }

    [Fact]
    public async Task GetNewPosts_WhenChildrenAreEmpty_ReturnsEmptyList()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", url = "/r/dotnet/new?limit=100" },
            response = new
            {
                status = 200,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                body = "{\"kind\":\"Listing\",\"data\":{\"children\":[]}}"
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new RedditHttpClient(httpClient, metrics);

        var result = await sut.GetNewPostsAsync("dotnet");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNewPosts_WhenStatusIsNotSuccess_ThrowsHttpRequestException()
    {
        await wireMock.ResetAsync();

        await wireMock.StubAsync(new
        {
            request = new { method = "GET", url = "/r/dotnet/new?limit=100" },
            response = new
            {
                status = (int)HttpStatusCode.TooManyRequests,
                headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                jsonBody = new { message = "Too Many Requests", error = 429 }
            }
        });

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(wireMock.BaseUrl);

        var metrics = new ScrapperMetrics();

        var sut = new RedditHttpClient(httpClient, metrics);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetNewPostsAsync("dotnet"));
    }
}
