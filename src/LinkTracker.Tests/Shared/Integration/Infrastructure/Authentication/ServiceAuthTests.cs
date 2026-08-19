using System.Net;
using LinkTracker.Shared.Infrastructure.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Tests.Shared.Integration.Infrastructure.Authentication;

[Trait("Module", "Shared")]
[Trait("Category", "Integration")]
public sealed class ServiceAuthTests
{
    private const string Secret = "b8a1c0d5-service-secret";
    private const string ProtectedPath = "/links";
    private const string PublicPath = "/metrics";

    [Fact]
    public async Task Get_WhenServiceTokenIsValid_AllowsRequest()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        using var response = await SendAsync(client, ProtectedPath, Secret);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_WhenServiceTokenIsMissing_RejectsRequest()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        using var response = await SendAsync(client, ProtectedPath, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WhenServiceTokenIsWrong_RejectsRequest()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        using var response = await SendAsync(client, ProtectedPath, "not-the-secret");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WhenEndpointIsNotProtected_AllowsAnonymousRequest()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        using var response = await SendAsync(client, PublicPath, token: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static TestServer CreateServer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ServiceAuth:Secret"] = Secret })
            .Build();

        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddServiceAuthentication(configuration);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints
                        .MapGet(ProtectedPath, () => Results.Ok())
                        .RequireServiceAuthorization();

                    endpoints.MapGet(PublicPath, () => Results.Ok());
                });
            }));
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (token is not null)
        {
            request.Headers.Add(ServiceAuthDefaults.HeaderName, token);
        }

        return await client.SendAsync(request);
    }
}
