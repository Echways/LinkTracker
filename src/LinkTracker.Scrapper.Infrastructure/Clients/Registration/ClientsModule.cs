using System.Net.Http.Headers;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using LinkTracker.Grpc;
using LinkTracker.Scrapper.Application.Clients.GitHub;
using LinkTracker.Scrapper.Application.Clients.StackOverflow;
using LinkTracker.Scrapper.Infrastructure.Clients.Bot;
using LinkTracker.Scrapper.Infrastructure.Clients.GitHub;
using LinkTracker.Scrapper.Infrastructure.Clients.RateLimiting;
using LinkTracker.Scrapper.Infrastructure.Clients.StackOverflow;
using LinkTracker.Scrapper.Infrastructure.Configuration.Bot;
using LinkTracker.Scrapper.Infrastructure.Configuration.Clients;
using LinkTracker.Scrapper.Infrastructure.Configuration.Kafka;
using LinkTracker.Scrapper.Infrastructure.Kafka.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Kafka.Serialization;
using LinkTracker.Scrapper.Infrastructure.Telemetry;
using LinkTracker.Shared.Infrastructure;
using LinkTracker.Shared.Infrastructure.Authentication;
using LinkTracker.Shared.Infrastructure.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.Scrapper.Infrastructure.Clients.Registration;

public static class ClientsModule
{
    private const string GitHubApiName = "github.com";
    private const string StackOverflowApiName = "stackoverflow.com";

    public static IServiceCollection AddClients(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<BotOptions>()
            .Bind(configuration.GetSection("Bot"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Bot:BaseUrl must be set")
            .ValidateOnStart();

        services
            .AddOptions<BotKafkaOptions>()
            .Bind(configuration.GetSection("Kafka"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka:BootstrapServers must be set")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Topic), "Kafka:Topic must be set")
            .ValidateOnStart();

        services
            .AddOptions<GitHubOptions>()
            .Bind(configuration.GetSection("GitHub"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "GitHub:BaseUrl must be set")
            .ValidateOnStart();

        services
            .AddOptions<StackOverflowOptions>()
            .Bind(configuration.GetSection("StackOverflow"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "StackOverflow:BaseUrl must be set")
            .ValidateOnStart();

        var httpResilienceOptions = configuration.GetHttpResilienceOptions();

        services.TryAddSingleton(TimeProvider.System);

        var gitHubRateLimit = configuration.GetSection("GitHub").Get<GitHubOptions>()?.RateLimit
                              ?? new GitHubOptions().RateLimit;

        var stackOverflowRateLimit = configuration.GetSection("StackOverflow").Get<StackOverflowOptions>()?.RateLimit
                                     ?? new StackOverflowOptions().RateLimit;

        services.AddExternalApiRateLimiter(GitHubApiName, gitHubRateLimit);
        services.AddExternalApiRateLimiter(StackOverflowApiName, stackOverflowRateLimit);

        services.AddHttpClient<BotHttpClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<BotOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            })
            .AddHttpMessageHandler<ServiceAuthHeaderHandler>()
            .AddConfiguredHttpResilience("scrapper-to-bot", httpResilienceOptions);

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BotOptions>>().Value;
            return GrpcChannel.ForAddress(options.BaseUrl);
        });

        services.AddSingleton(sp =>
        {
            var invoker = sp.GetRequiredService<GrpcChannel>()
                .CreateCallInvoker()
                .Intercept(sp.GetRequiredService<ServiceAuthClientInterceptor>());

            return new BotUpdatesGrpc.BotUpdatesGrpcClient(invoker);
        });

        services.AddSingleton<BotGrpcClient>();

        services.AddSingleton<IProducer<string, byte[]>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BotKafkaOptions>>().Value;

            var config = new ProducerConfig { BootstrapServers = options.BootstrapServers, Acks = Acks.All, EnableIdempotence = true };

            return new ProducerBuilder<string, byte[]>(config).Build();
        });

        services.AddSingleton<ISchemaRegistryClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BotKafkaOptions>>().Value;

            return new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = options.SchemaRegistryUrl });
        });

        services.AddSingleton<ILinkUpdateKafkaSerializer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BotKafkaOptions>>().Value;

            return options.Serialization switch
            {
                KafkaSerializationKind.Json => new JsonLinkUpdateKafkaSerializer(),
                KafkaSerializationKind.Avro => new AvroLinkUpdateKafkaSerializer(
                    new AvroSerializer<GenericRecord>(
                        sp.GetRequiredService<ISchemaRegistryClient>())),
                _ => throw new InvalidOperationException($"Unsupported Kafka serialization: {options.Serialization}")
            };
        });

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<BotKafkaOptions>>().Value);
        services.AddSingleton<BotKafkaClient>();

        services.AddTransient<IBotTransportClient>(sp => sp.GetRequiredService<BotHttpClient>());
        services.AddTransient<IBotTransportClient>(sp => sp.GetRequiredService<BotGrpcClient>());
        services.AddTransient<IBotTransportClient>(sp => sp.GetRequiredService<BotKafkaClient>());
        services.AddTransient<IBotDirectClient, FallbackBotClient>();

        services.AddHttpClient<IGitHubClient, GitHubHttpClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LinkTracker", "1.0"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                if (!string.IsNullOrWhiteSpace(options.Token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
                }
            })
            .AddConfiguredHttpResilience("github", httpResilienceOptions)
            .AddExternalApiRateLimiting(GitHubApiName, gitHubRateLimit);

        services.AddHttpClient<IStackOverflowClient, StackOverflowHttpClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<StackOverflowOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LinkTracker", "1.0"));
            })
            .AddConfiguredHttpResilience("stackoverflow", httpResilienceOptions)
            .AddExternalApiRateLimiting(StackOverflowApiName, stackOverflowRateLimit);

        return services;
    }

    private static IServiceCollection AddExternalApiRateLimiter(
        this IServiceCollection services,
        string apiName,
        ExternalApiRateLimitOptions options)
    {
        if (!options.Enabled)
        {
            return services;
        }

        services.AddKeyedSingleton(
            apiName,
            (sp, _) => new ExternalApiRateLimiter(apiName, options, sp.GetRequiredService<TimeProvider>()));

        return services;
    }

    private static IHttpClientBuilder AddExternalApiRateLimiting(
        this IHttpClientBuilder builder,
        string apiName,
        ExternalApiRateLimitOptions options)
    {
        if (!options.Enabled)
        {
            return builder;
        }

        return builder.AddHttpMessageHandler(sp => new ExternalApiRateLimitingHandler(
            sp.GetRequiredKeyedService<ExternalApiRateLimiter>(apiName),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ScrapperMetrics>(),
            sp.GetRequiredService<ILogger<ExternalApiRateLimitingHandler>>()));
    }
}