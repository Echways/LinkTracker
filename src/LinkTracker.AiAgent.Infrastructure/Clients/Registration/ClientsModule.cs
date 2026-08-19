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
            .Validate(o => o.Filtering.MinLength >= 0, "AiAgent:Filtering:MinLength must not be negative")
            .Validate(o => o.Summarization.Threshold > 0, "AiAgent:Summarization:Threshold must be greater than 0")
            .Validate(o => o.Grouping.WindowMs > 0, "AiAgent:Grouping:WindowMs must be greater than 0")
            .Validate(o => o.Grouping.FlushIntervalMs > 0, "AiAgent:Grouping:FlushIntervalMs must be greater than 0")
            .ValidateOnStart();

        services
            .AddOptions<RawUpdatesKafkaOptions>()
            .Bind(configuration.GetSection("Kafka:Consumer"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka:Consumer:BootstrapServers must be set")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Topic), "Kafka:Consumer:Topic must be set")
            .Validate(o => !string.IsNullOrWhiteSpace(o.GroupId), "Kafka:Consumer:GroupId must be set")
            .Validate(o => !string.IsNullOrWhiteSpace(o.DeadLetterTopic), "Kafka:Consumer:DeadLetterTopic must be set")
            .Validate(o => o.RetryAttempts > 0, "Kafka:Consumer:RetryAttempts must be positive")
            .Validate(o => o.RetryBackoffMilliseconds >= 0, "Kafka:Consumer:RetryBackoffMilliseconds must not be negative")
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
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "YandexAi:ApiKey must be set")
            .Validate(o => !string.IsNullOrWhiteSpace(o.FolderId), "YandexAi:FolderId must be set")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ModelId), "YandexAi:ModelId must be set")
            .Validate(
                o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _),
                "YandexAi:BaseUrl must be an absolute URI")
            .Validate(o => o.TimeoutSeconds > 0, "YandexAi:TimeoutSeconds must be greater than 0")
            .ValidateOnStart();

        services.AddSingleton<KafkaOffsetTracker>();

        services.AddSingleton<IConsumer<string, byte[]>>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RawUpdatesKafkaOptions>>().Value;
            var offsetTracker = sp.GetRequiredService<KafkaOffsetTracker>();

            return new ConsumerBuilder<string, byte[]>(new ConsumerConfig
            {
                BootstrapServers = opts.BootstrapServers,
                GroupId = opts.GroupId,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                AllowAutoCreateTopics = false
            })
            .SetPartitionsRevokedHandler((_, partitions) =>
                offsetTracker.Forget(partitions.Select(x => x.TopicPartition)))
            .Build();
        });

        services.AddSingleton<IProducer<string, byte[]>>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<ProcessedUpdatesKafkaOptions>>().Value;
            return new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = opts.BootstrapServers, Acks = Acks.All, EnableIdempotence = true, AllowAutoCreateTopics = false }).Build();
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

        services.AddSingleton<IRawUpdateDeadLetterPublisher, RawUpdatesKafkaDeadLetterPublisher>();
        services.AddSingleton<IRawUpdatesKafkaMessageHandler, RawUpdatesKafkaMessageHandler>();
        services.AddHostedService<RawUpdatesKafkaConsumer>();

        services.AddSingleton<ILinkUpdatePrioritizer, KeywordLinkUpdatePrioritizer>();
        services.AddSingleton<ILinkUpdateGrouper, WindowLinkUpdateGrouper>();
        services.AddSingleton<IGroupingBuffer, TimeWindowGroupingBuffer>();

        services.AddHostedService<GroupingFlushJob>();

        return services;
    }
}