using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using LinkTracker.Bot.Application.Updates.Abstractions;
using LinkTracker.Bot.Presentation.Grpc;
using LinkTracker.Grpc;
using LinkTracker.Shared.Contracts.Bot;
using LinkTracker.Shared.Infrastructure.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LinkTracker.Tests.Shared.Integration.Infrastructure.Authentication;

[Trait("Module", "Shared")]
[Trait("Category", "Integration")]
public sealed class GrpcServiceAuthTests
{
    private const string Secret = "9f2e4c17-grpc-service-secret";

    [Fact]
    public async Task SendUpdate_WhenClientAttachesServiceToken_IsHandled()
    {
        var notifier = Substitute.For<ILinkUpdateNotifier>();

        using var server = CreateServer(notifier);
        var client = CreateClient(server, Secret);

        await client.SendUpdateAsync(CreateRequest());

        await notifier.Received(1).NotifyAsync(Arg.Any<LinkUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendUpdate_WhenServiceTokenIsMissing_IsRejected()
    {
        var notifier = Substitute.For<ILinkUpdateNotifier>();

        using var server = CreateServer(notifier);
        var client = CreateClient(server, token: null);

        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await client.SendUpdateAsync(CreateRequest()));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);

        await notifier.DidNotReceive().NotifyAsync(Arg.Any<LinkUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendUpdate_WhenServiceTokenIsWrong_IsRejected()
    {
        var notifier = Substitute.For<ILinkUpdateNotifier>();

        using var server = CreateServer(notifier);
        var client = CreateClient(server, "not-the-secret");

        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await client.SendUpdateAsync(CreateRequest()));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);

        await notifier.DidNotReceive().NotifyAsync(Arg.Any<LinkUpdate>(), Arg.Any<CancellationToken>());
    }

    private static TestServer CreateServer(ILinkUpdateNotifier notifier)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ServiceAuth:Secret"] = Secret })
            .Build();

        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddGrpc();
                services.AddSingleton(notifier);
                services.AddServiceAuthentication(configuration);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                    endpoints.MapGrpcService<BotUpdatesGrpcService>().RequireServiceAuthorization());
            }));
    }

    private static BotUpdatesGrpc.BotUpdatesGrpcClient CreateClient(TestServer server, string? token)
    {
        var channel = GrpcChannel.ForAddress(
            server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = server.CreateHandler() });

        if (token is null)
        {
            return new BotUpdatesGrpc.BotUpdatesGrpcClient(channel);
        }

        var interceptor = new ServiceAuthClientInterceptor(Options.Create(new ServiceAuthOptions { Secret = token }));

        return new BotUpdatesGrpc.BotUpdatesGrpcClient(channel.CreateCallInvoker().Intercept(interceptor));
    }

    private static LinkUpdateGrpcRequest CreateRequest()
    {
        var request = new LinkUpdateGrpcRequest { Id = 1, Url = "https://github.com/user/repo", Description = "update" };

        request.TgChatIds.Add(42);

        return request;
    }
}
