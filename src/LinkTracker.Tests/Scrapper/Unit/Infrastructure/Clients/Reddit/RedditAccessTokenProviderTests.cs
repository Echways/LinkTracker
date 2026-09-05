using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LinkTracker.Scrapper.Infrastructure.Clients.Reddit;
using LinkTracker.Scrapper.Infrastructure.Configuration.Clients;
using Microsoft.Extensions.Options;

namespace LinkTracker.Tests.Scrapper.Unit.Infrastructure.Clients.Reddit;

[Trait("Module", "Scrapper")]
[Trait("Category", "Unit")]
public sealed class RedditAccessTokenProviderTests
{
    private const string TokenUrl = "https://www.reddit.com/api/v1/access_token";

    [Fact]
    public async Task GetAccessTokenAsync_WhenCalledTwice_RequestsTokenOnce()
    {
        var handler = new StubHttpMessageHandler(expiresInSeconds: 3600);
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));

        var sut = CreateSut(handler, timeProvider);

        var first = await sut.GetAccessTokenAsync();
        var second = await sut.GetAccessTokenAsync();

        Assert.Equal("token-1", first);
        Assert.Equal("token-1", second);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenIsAboutToExpire_RequestsNewToken()
    {
        var handler = new StubHttpMessageHandler(expiresInSeconds: 3600);
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));

        var sut = CreateSut(handler, timeProvider);

        var first = await sut.GetAccessTokenAsync();

        timeProvider.Advance(TimeSpan.FromSeconds(3600 - 30));

        var second = await sut.GetAccessTokenAsync();

        Assert.Equal("token-1", first);
        Assert.Equal("token-2", second);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_SendsBasicAuthorizationAndClientCredentialsGrant()
    {
        var handler = new StubHttpMessageHandler(expiresInSeconds: 3600);
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));

        var sut = CreateSut(handler, timeProvider);

        await sut.GetAccessTokenAsync();

        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("client-id:client-secret"));

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(new Uri(TokenUrl), handler.LastRequestUri);
        Assert.Equal("Basic", handler.LastAuthorization?.Scheme);
        Assert.Equal(expectedCredentials, handler.LastAuthorization?.Parameter);
        Assert.Equal("grant_type=client_credentials", handler.LastBody);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenStatusIsNotSuccess_ThrowsHttpRequestException()
    {
        var handler = new StubHttpMessageHandler(expiresInSeconds: 3600) { StatusCode = HttpStatusCode.Unauthorized };
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));

        var sut = CreateSut(handler, timeProvider);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetAccessTokenAsync());
    }

    private static RedditAccessTokenProvider CreateSut(StubHttpMessageHandler handler, TimeProvider timeProvider)
    {
        var options = Options.Create(new RedditOptions { TokenUrl = TokenUrl, ClientId = "client-id", ClientSecret = "client-secret" });

        return new RedditAccessTokenProvider(new StubHttpClientFactory(handler), options, timeProvider);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private sealed class StubHttpMessageHandler(int expiresInSeconds) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;

        public HttpMethod? LastMethod { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;

            LastMethod = request.Method;
            LastRequestUri = request.RequestUri;
            LastAuthorization = request.Headers.Authorization;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(
                    $"{{\"access_token\":\"token-{RequestCount}\",\"token_type\":\"bearer\",\"expires_in\":{expiresInSeconds}}}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan delta)
        {
            _utcNow = _utcNow.Add(delta);
        }
    }
}
