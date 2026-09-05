using LinkTracker.Scrapper.Application.Clients.Reddit;
using LinkTracker.Scrapper.Infrastructure.Clients.Reddit;
using LinkTracker.Scrapper.Infrastructure.Clients.Registration;
using LinkTracker.Scrapper.Infrastructure.Configuration.Clients;
using LinkTracker.Scrapper.Infrastructure.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkTracker.Tests.Scrapper.Unit.Infrastructure.Clients.Registration;

[Trait("Module", "Scrapper")]
[Trait("Category", "Unit")]
public sealed class ClientsModuleRedditTests
{
    [Fact]
    public void AddClients_ResolvesRedditHttpClient()
    {
        using var serviceProvider = BuildServiceProvider();

        var client = serviceProvider.GetRequiredService<IRedditClient>();

        Assert.IsType<RedditHttpClient>(client);
    }

    [Fact]
    public void AddClients_BindsRedditOptionsFromConfiguration()
    {
        using var serviceProvider = BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<RedditOptions>>().Value;

        Assert.Equal("https://oauth.reddit.com", options.BaseUrl);
        Assert.Equal("https://www.reddit.com/api/v1/access_token", options.TokenUrl);
        Assert.Equal("client-id", options.ClientId);
        Assert.Equal("client-secret", options.ClientSecret);
        Assert.Equal(100, options.RateLimit.TokenLimit);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bot:BaseUrl"] = "http://localhost:8091",
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:Topic"] = "link.raw-updates",
                ["Reddit:ClientId"] = "client-id",
                ["Reddit:ClientSecret"] = "client-secret"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ScrapperMetrics>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddClients(configuration);

        return services.BuildServiceProvider();
    }
}
