using System.Net;
using LinkTracker.Shared.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Tests.Shared.Integration.Infrastructure.RateLimiting;

[Trait("Module", "Shared")]
[Trait("Category", "Integration")]
public sealed class ApiRateLimitingTests
{
    private const string RemoteIpAddressHeaderName = "X-Test-Remote-Ip";
    private const string ChatIdHeaderName = "Tg-Chat-Id";
    private const string LimitedPath = "/links";
    private const string UnlimitedPath = "/metrics";
    private const string FirstIpAddress = "127.0.0.1";
    private const string SecondIpAddress = "127.0.0.2";

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Get_WhenPermitLimitConfigured_AllowsRequestsUntilLimit(int permitLimit)
    {
        using var server = CreateServer(settings =>
        {
            settings["RateLimiting:PermitLimit"] = permitLimit.ToString();
        });

        using var client = server.CreateClient();

        for (var i = 0; i < permitLimit; i++)
        {
            using var allowed = await SendAsync(client, LimitedPath, FirstIpAddress, chatId: "100");

            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var rejected = await SendAsync(client, LimitedPath, FirstIpAddress, chatId: "100");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task Get_WhenChatIdsDifferButIpIsShared_UsesIndependentLimits()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        using var firstChatFirstCall = await SendAsync(client, LimitedPath, FirstIpAddress, chatId: "100");
        using var firstChatSecondCall = await SendAsync(client, LimitedPath, FirstIpAddress, chatId: "100");
        using var secondChatFirstCall = await SendAsync(client, LimitedPath, FirstIpAddress, chatId: "200");

        Assert.Equal(HttpStatusCode.OK, firstChatFirstCall.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, firstChatSecondCall.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondChatFirstCall.StatusCode);
    }

    [Fact]
    public async Task Get_WhenChatIdHeaderIsMissing_FallsBackToRemoteIpPartition()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        using var firstIpFirstCall = await SendAsync(client, LimitedPath, FirstIpAddress);
        using var firstIpSecondCall = await SendAsync(client, LimitedPath, FirstIpAddress);
        using var secondIpFirstCall = await SendAsync(client, LimitedPath, SecondIpAddress);

        Assert.Equal(HttpStatusCode.OK, firstIpFirstCall.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, firstIpSecondCall.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondIpFirstCall.StatusCode);
    }

    [Fact]
    public async Task Get_WhenRemoteIpIsTrusted_IsNotThrottled()
    {
        using var server = CreateServer(settings =>
        {
            settings["RateLimiting:TrustedNetworks:0"] = "127.0.0.0/8";
        });

        using var client = server.CreateClient();

        using var first = await SendAsync(client, LimitedPath, FirstIpAddress, chatId: "100");
        using var second = await SendAsync(client, LimitedPath, FirstIpAddress, chatId: "100");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Get_WhenEndpointDoesNotRequirePolicy_IsNotThrottled()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        using var first = await SendAsync(client, UnlimitedPath, FirstIpAddress);
        using var second = await SendAsync(client, UnlimitedPath, FirstIpAddress);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    private static TestServer CreateServer(Action<Dictionary<string, string?>>? configureSettings = null)
    {
        var settings = new Dictionary<string, string?> { ["RateLimiting:PermitLimit"] = "1", ["RateLimiting:WindowSeconds"] = "60", ["RateLimiting:SegmentsPerWindow"] = "1", ["RateLimiting:QueueLimit"] = "0" };

        configureSettings?.Invoke(settings);

        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(settings)
                    .Build();

                services.AddRouting();
                services.AddApiRateLimiting(configuration);
            })
            .Configure(app =>
            {
                app.Use(SetRemoteIpAddressFromHeader);
                app.UseRouting();
                app.UseRateLimiter();
                app.UseEndpoints(endpoints =>
                {
                    endpoints
                        .MapGet(LimitedPath, () => Results.Ok())
                        .RequireRateLimiting(RateLimitingPolicies.PublicApi);

                    endpoints.MapGet(UnlimitedPath, () => Results.Ok());
                });
            }));
    }

    private static Task SetRemoteIpAddressFromHeader(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Headers.TryGetValue(RemoteIpAddressHeaderName, out var values)
            && IPAddress.TryParse(values.ToString(), out var ipAddress))
        {
            context.Connection.RemoteIpAddress = ipAddress;
        }

        return next(context);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string path,
        string remoteIpAddress,
        string? chatId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        request.Headers.Add(RemoteIpAddressHeaderName, remoteIpAddress);

        if (chatId is not null)
        {
            request.Headers.Add(ChatIdHeaderName, chatId);
        }

        return await client.SendAsync(request);
    }
}
