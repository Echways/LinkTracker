using System.Net;
using LinkTracker.Scrapper.Infrastructure.Clients.Reddit;
using NSubstitute;

namespace LinkTracker.Tests.Scrapper.Unit.Infrastructure.Clients.Reddit;

[Trait("Module", "Scrapper")]
[Trait("Category", "Unit")]
public sealed class RedditAccessTokenHandlerTests
{
    [Fact]
    public async Task SendAsync_AddsBearerAuthorizationHeader()
    {
        var tokenProvider = Substitute.For<IRedditAccessTokenProvider>();
        tokenProvider.GetAccessTokenAsync(Arg.Any<CancellationToken>()).Returns("token-1");

        var innerHandler = new CapturingHandler();

        var sut = new RedditAccessTokenHandler(tokenProvider) { InnerHandler = innerHandler };

        using var invoker = new HttpMessageInvoker(sut);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://oauth.reddit.com/r/dotnet/new");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", innerHandler.LastAuthorizationScheme);
        Assert.Equal("token-1", innerHandler.LastAuthorizationParameter);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastAuthorizationScheme { get; private set; }

        public string? LastAuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
            LastAuthorizationParameter = request.Headers.Authorization?.Parameter;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
