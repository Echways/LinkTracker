using LinkTracker.Shared.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Tests.Shared.Integration.Infrastructure.Telemetry;

[Trait("Module", "Shared")]
[Trait("Category", "Integration")]
public sealed class HttpRouteLabelTests
{
    [Fact]
    public async Task Resolve_WhenRouteHasParameters_ReturnsRouteTemplateInsteadOfRawPath()
    {
        var labels = new List<string>();

        using var server = CreateServer(labels);
        using var client = server.CreateClient();

        using var first = await client.PostAsync("/tg-chat/1", content: null);
        using var second = await client.PostAsync("/tg-chat/2", content: null);

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();

        Assert.Equal(["/tg-chat/{id:long}", "/tg-chat/{id:long}"], labels);
    }

    [Fact]
    public async Task Resolve_WhenRequestMatchesNoEndpoint_ReturnsUnmatchedLabel()
    {
        var labels = new List<string>();

        using var server = CreateServer(labels);
        using var client = server.CreateClient();

        using var response = await client.GetAsync("/does-not-exist");

        Assert.Equal([HttpRouteLabel.Unmatched], labels);
    }

    private static TestServer CreateServer(List<string> labels)
    {
        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services => services.AddRouting())
            .Configure(app =>
            {
                app.UseRouting();
                app.Use(async (context, next) =>
                {
                    labels.Add(HttpRouteLabel.Resolve(context));
                    await next(context);
                });
                app.UseEndpoints(endpoints => endpoints.MapPost("/tg-chat/{id:long}", (long id) => Results.Ok(id)));
            }));
    }
}
