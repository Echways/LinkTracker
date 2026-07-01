using System.Net;
using LinkTracker.Shared.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace LinkTracker.Tests.Shared.Integration.Infrastructure.RateLimiting;

[Trait("Module", "Shared")]
[Trait("Category", "Integration")]
public sealed class IpRateLimitingTests
{
    private const string RemoteIpAddressHeaderName = "X-Test-Remote-Ip";
    private const string FirstIpAddress = "127.0.0.1";
    private const string SecondIpAddress = "127.0.0.2";
    private const string MissingRemoteIpAddressHeaderValue = "none";

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task GetAsync_WhenPermitLimitConfigured_AllowsRequestsUntilLimit(int permitLimit)
    {
        using var server = CreateServer(settings =>
        {
            settings["RateLimiting:PermitLimit"] = permitLimit.ToString();
        });
        using var client = server.CreateClient();

        var allowedResponses = new List<HttpResponseMessage>();

        try
        {
            for (var i = 0; i < permitLimit; i++)
            {
                allowedResponses.Add(await GetAsync(client, FirstIpAddress));
            }

            using var rejectedResponse = await GetAsync(client, FirstIpAddress);

            Assert.All(allowedResponses, response =>
                Assert.Equal(HttpStatusCode.OK, response.StatusCode));

            Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        }
        finally
        {
            foreach (var response in allowedResponses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task GetAsync_WhenRequestsComeFromDifferentIps_UsesIndependentLimits()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        using var firstIpFirstResponse = await GetAsync(client, FirstIpAddress);
        using var firstIpSecondResponse = await GetAsync(client, FirstIpAddress);
        using var secondIpFirstResponse = await GetAsync(client, SecondIpAddress);

        Assert.Equal(HttpStatusCode.OK, firstIpFirstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, firstIpSecondResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondIpFirstResponse.StatusCode);
    }

    [Fact]
    public async Task GetAsync_WhenRemoteIpAddressIsMissing_UsesSharedUnknownPartition()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        using var firstResponse = await GetWithoutRemoteIpAddressAsync(client);
        using var secondResponse = await GetWithoutRemoteIpAddressAsync(client);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
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

                services.AddIpRateLimiting(configuration);
            })
            .Configure(app =>
            {
                app.Use(SetRemoteIpAddressFromHeader);
                app.UseRateLimiter();
                app.Run(context => context.Response.WriteAsync("ok"));
            }));
    }

    private static Task SetRemoteIpAddressFromHeader(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Headers.TryGetValue(RemoteIpAddressHeaderName, out var values))
        {
            var rawIpAddress = values.ToString();

            if (rawIpAddress == MissingRemoteIpAddressHeaderValue)
            {
                context.Connection.RemoteIpAddress = null;
            }
            else if (IPAddress.TryParse(rawIpAddress, out var ipAddress))
            {
                context.Connection.RemoteIpAddress = ipAddress;
            }
        }

        return next(context);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string ipAddress)
    {
        return GetWithRemoteIpAddressHeaderAsync(client, ipAddress);
    }

    private static Task<HttpResponseMessage> GetWithoutRemoteIpAddressAsync(HttpClient client)
    {
        return GetWithRemoteIpAddressHeaderAsync(client, MissingRemoteIpAddressHeaderValue);
    }

    private static async Task<HttpResponseMessage> GetWithRemoteIpAddressHeaderAsync(
        HttpClient client,
        string remoteIpAddressHeaderValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(RemoteIpAddressHeaderName, remoteIpAddressHeaderValue);

        return await client.SendAsync(request);
    }
}