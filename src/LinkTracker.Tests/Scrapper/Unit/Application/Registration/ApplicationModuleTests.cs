using LinkTracker.Scrapper.Application.Abstractions.Updates;
using LinkTracker.Scrapper.Application.Clients.GitHub;
using LinkTracker.Scrapper.Application.Clients.Reddit;
using LinkTracker.Scrapper.Application.Clients.StackOverflow;
using LinkTracker.Scrapper.Application.Registration;
using LinkTracker.Scrapper.Application.Services.Updates.Clients;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LinkTracker.Tests.Scrapper.Unit.Application.Registration;

[Trait("Module", "Scrapper")]
[Trait("Category", "Unit")]
public sealed class ApplicationModuleTests
{
    [Fact]
    public void AddApplication_RegistersHandlerForEverySupportedSource()
    {
        using var serviceProvider = BuildServiceProvider();

        var handlers = serviceProvider.GetServices<ILinkUpdateHandler>().ToArray();

        Assert.Contains(handlers, x => x is GitHubLinkUpdateHandler);
        Assert.Contains(handlers, x => x is StackOverflowLinkUpdateHandler);
        Assert.Contains(handlers, x => x is RedditLinkUpdateHandler);
    }

    [Fact]
    public void AddApplication_ResolvedHandlers_AcceptSubredditLink()
    {
        using var serviceProvider = BuildServiceProvider();

        var handlers = serviceProvider.GetServices<ILinkUpdateHandler>();

        Assert.Contains(handlers, x => x.CanHandle(new Uri("https://www.reddit.com/r/dotnet")));
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(Substitute.For<IGitHubClient>());
        services.AddSingleton(Substitute.For<IStackOverflowClient>());
        services.AddSingleton(Substitute.For<IRedditClient>());
        services.AddSingleton(Substitute.For<ILinkTrackingStore>());

        services.AddApplication();

        return services.BuildServiceProvider();
    }
}
