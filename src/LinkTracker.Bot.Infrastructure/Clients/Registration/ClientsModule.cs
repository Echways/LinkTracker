using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Infrastructure.Abstractions.Kafka;
using LinkTracker.Bot.Infrastructure.Clients.Kafka;
using LinkTracker.Bot.Infrastructure.Clients.Scrapper;
using LinkTracker.Bot.Infrastructure.Configuration;
using LinkTracker.Bot.Infrastructure.Configuration.Kafka;
using LinkTracker.Bot.Infrastructure.Kafka.Abstractions;
using LinkTracker.Bot.Infrastructure.Kafka.Deserialization;
using LinkTracker.Grpc;
using LinkTracker.Shared.Infrastructure;
using LinkTracker.Shared.Infrastructure.Authentication;
using LinkTracker.Shared.Infrastructure.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LinkTracker.Bot.Infrastructure.Clients.Registration;

public static class ClientsModule
{
    public static IServiceCollection AddClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ScrapperOptions>()
            .Bind(configuration.GetSection("Scrapper"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Scrapper:BaseUrl must be set")
            .ValidateOnStart();

        services
            .AddOptions<LinkUpdatesKafkaOptions>()
            .Bind(configuration.GetSection("Kafka"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka:BootstrapServers must be set")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Topic), "Kafka:Topic must be set")
            .Validate(o => !string.IsNullOrWhiteSpace(o.GroupId), "Kafka:GroupId must be set")
            .Validate(o => !string.IsNullOrWhiteSpace(o.DeadLetterTopic), "Kafka:DeadLetterTopic must be set")
            .Validate(o => o.RetryAttempts > 0, "Kafka:RetryAttempts must be positive")
            .Validate(o => o.RetryBackoffMilliseconds >= 0, "Kafka:RetryBackoffMilliseconds must not be negative")
            .ValidateOnStart();

        var httpResilienceOptions = configuration.GetHttpResilienceOptions();

        services.AddHttpClient<ScrapperHttpClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<ScrapperOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            })
            .AddHttpMessageHandler<ServiceAuthHeaderHandler>()
            .AddConfiguredHttpResilience("bot-to-scrapper", httpResilienceOptions);

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ScrapperOptions>>().Value;
            return GrpcChannel.ForAddress(options.BaseUrl);
        });

        services.AddSingleton(sp =>
        {
            var invoker = sp.GetRequiredService<GrpcChannel>()
                .CreateCallInvoker()
                .Intercept(sp.GetRequiredService<ServiceAuthClientInterceptor>());

            return new ScrapperGrpc.ScrapperGrpcClient(invoker);
        });

        services.AddSingleton<ScrapperGrpcClient>();

        services.AddSingleton<IConsumer<string, byte[]>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LinkUpdatesKafkaOptions>>().Value;

            var config = new ConsumerConfig
            {
                BootstrapServers = options.BootstrapServers,
                GroupId = options.GroupId,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                AllowAutoCreateTopics = false
            };

            return new ConsumerBuilder<string, byte[]>(config).Build();
        });

        services.AddSingleton<IProducer<Null, string>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LinkUpdatesKafkaOptions>>().Value;

            var config = new ProducerConfig { BootstrapServers = options.BootstrapServers, Acks = Acks.All, EnableIdempotence = true, AllowAutoCreateTopics = false };

            return new ProducerBuilder<Null, string>(config).Build();
        });

        services.AddSingleton<ISchemaRegistryClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LinkUpdatesKafkaOptions>>().Value;

            return new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = options.SchemaRegistryUrl });
        });

        services.AddSingleton<ILinkUpdateKafkaDeserializer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LinkUpdatesKafkaOptions>>().Value;

            return options.Serialization switch
            {
                KafkaSerializationKind.Json => new JsonLinkUpdateKafkaDeserializer(),
                KafkaSerializationKind.Avro => new AvroLinkUpdateKafkaDeserializer(
                    new AvroDeserializer<GenericRecord>(
                        sp.GetRequiredService<ISchemaRegistryClient>())),
                _ => throw new InvalidOperationException($"Unsupported Kafka serialization: {options.Serialization}")
            };
        });

        services.AddSingleton<KafkaLinkUpdateMessageParser>();
        services.AddSingleton<ILinkUpdateDeadLetterPublisher, KafkaLinkUpdateDeadLetterPublisher>();
        services.AddSingleton<ILinkUpdatesKafkaMessageHandler, LinkUpdatesKafkaMessageHandler>();
        services.AddHostedService<LinkUpdatesKafkaConsumer>();

        services.AddSingleton<IScrapperClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ScrapperOptions>>().Value;

            return options.Transport switch
            {
                TransportKind.Http => sp.GetRequiredService<ScrapperHttpClient>(),
                TransportKind.Grpc => sp.GetRequiredService<ScrapperGrpcClient>(),
                _ => throw new InvalidOperationException($"Unsupported transport: {options.Transport}")
            };
        });

        return services;
    }
}