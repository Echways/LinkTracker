using Confluent.Kafka;
using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Clients.Kafka;
using LinkTracker.AiAgent.Infrastructure.Clients.YandexAi;
using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.AiAgent.Infrastructure.Configuration.Kafka;
using LinkTracker.AiAgent.Infrastructure.Configuration.YandexAi;
using LinkTracker.AiAgent.Infrastructure.Kafka.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Kafka.Deserialization;
using LinkTracker.AiAgent.Infrastructure.Kafka.Serialization;
using LinkTracker.AiAgent.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LinkTracker.AiAgent.Infrastructure.Clients.Registration;

public static class ClientsModule
{
    public static IServiceCollection AddAiAgentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AiAgentOptions>()
            .Bind(configuration.GetSection("AiAgent"))
            .Validate(o => o.Filtering != null, "AiAgent:Filtering must be set")
            .Validate(o => o.Summarization != null, "AiAgent:Summarization must be set")
            .Validate(o => o.Prioritization != null, "AiAgent:Prioritization must be set")
            .Validate(o => o.Grouping.WindowMs > 0, "AiAgent:Grouping:WindowMs must be greater that 0")
            .Validate(o => o.Grouping.FlushIntervalMs > 0, "AiAgent:Grouping.FlushIntervalMs must be greater that 0")
            .ValidateOnStart();

        services
            .AddOptions<RawUpdatesKafkaOptions>()
            .Bind(configuration.GetSection("Kafka:Consumer"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka:Consumer:BootstrapServers must be set")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Topic), "Kafka:Consumer:Topic must be set")
            .Validate(o => !string.IsNullOrWhiteSpace(o.GroupId), "Kafka:Consumer:GroupId must be set")
            .ValidateOnStart();

        services
            .AddOptions<ProcessedUpdatesKafkaOptions>()
            .Bind(configuration.GetSection("Kafka:Producer"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka:Producer:BootstrapServers must be set")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Topic), "Kafka:Producer:Topic must be set")
            .ValidateOnStart();

        services
            .AddOptions<YandexAiOptions>()
            .Bind(configuration.GetSection("YandexAi"))
            .ValidateOnStart();

        services.AddSingleton<IConsumer<string, byte[]>>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RawUpdatesKafkaOptions>>().Value;
            return new ConsumerBuilder<string, byte[]>(new ConsumerConfig
            {
                BootstrapServers = opts.BootstrapServers,
                GroupId = opts.GroupId,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                AllowAutoCreateTopics = false
            }).Build();
        });

        services.AddSingleton<IProducer<string, byte[]>>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<ProcessedUpdatesKafkaOptions>>().Value;
            return new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = opts.BootstrapServers, Acks = Acks.All, AllowAutoCreateTopics = false }).Build();
        });

        services.AddHttpClient(nameof(YandexAiHttpClient), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<YandexAiOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        services.AddSingleton<ILinkUpdateSummarizer, YandexAiHttpClient>();

        services.AddSingleton<IRawLinkUpdateKafkaDeserializer, JsonRawLinkUpdateKafkaDeserializer>();
        services.AddSingleton<IProcessedLinkUpdateKafkaSerializer, JsonProcessedLinkUpdateKafkaSerializer>();

        services.AddSingleton<ILinkUpdateFilter, LinkUpdateFilter>();
        services.AddSingleton<IProcessedUpdatePublisher, ProcessedUpdatesKafkaPublisher>();

        services.AddSingleton<IRawUpdatesKafkaMessageHandler, RawUpdatesKafkaMessageHandler>();
        services.AddHostedService<RawUpdatesKafkaConsumer>();

        services.AddSingleton<ILinkUpdatePrioritizer, KeywordLinkUpdatePrioritizer>();
        services.AddSingleton<ILinkUpdateGrouper, WindowLinkUpdateGrouper>();
        services.AddSingleton<IGroupingBuffer, TimeWindowGroupingBuffer>();
        services.AddHostedService<GroupingFlushJob>();

        return services;
    }
}