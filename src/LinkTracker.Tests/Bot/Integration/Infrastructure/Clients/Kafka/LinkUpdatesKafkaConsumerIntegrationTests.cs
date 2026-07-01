using System.Text;
using System.Text.Json;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Bot.Application.Updates.Abstractions;
using LinkTracker.Bot.Infrastructure.Abstractions.Kafka;
using LinkTracker.Bot.Infrastructure.Clients.Kafka;
using LinkTracker.Bot.Infrastructure.Configuration.Kafka;
using LinkTracker.Bot.Infrastructure.Kafka.Deserialization;
using LinkTracker.Bot.Infrastructure.Models.Kafka;
using LinkTracker.Scrapper.Infrastructure.Clients.Bot;
using LinkTracker.Scrapper.Infrastructure.Configuration.Kafka;
using LinkTracker.Scrapper.Infrastructure.Kafka.Serialization;
using LinkTracker.Scrapper.Infrastructure.Telemetry;
using LinkTracker.Shared.Contracts.Bot;
using LinkTracker.Shared.Infrastructure;
using LinkTracker.Tests.Bot.Integration.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LinkTracker.Tests.Bot.Integration.Infrastructure.Clients.Kafka;

[Collection("Kafka collection")]
[Trait("Module", "Bot")]
[Trait("Category", "Integration")]
public sealed class LinkUpdatesKafkaConsumerIntegrationTests(KafkaTestContainerFixture kafkaFixture)
{
    [Fact]
    public async Task Consumer_WhenMessageIsPublishedToKafka_NotifiesUser()
    {
        var topic = $"link-updates-{Guid.NewGuid():N}";
        var deadLetterTopic = $"link-updates-dlq-{Guid.NewGuid():N}";

        await kafkaFixture.CreateTopicAsync(topic);
        await kafkaFixture.CreateTopicAsync(deadLetterTopic);

        var notifier = Substitute.For<ILinkUpdateNotifier>();
        var deadLetterPublisher = Substitute.For<ILinkUpdateDeadLetterPublisher>();

        using var consumer = CreateConsumer(
            topic,
            deadLetterTopic,
            notifier,
            deadLetterPublisher);

        await consumer.StartAsync(CancellationToken.None);

        try
        {
            await ProduceAsync(
                topic,
                """
                {
                  "id": 42,
                  "url": "https://github.com/user/repo",
                  "description": "Repository updated",
                  "tgChatIds": [123]
                }
                """);

            await WaitUntilAsync(async () =>
            {
                try
                {
                    await notifier.Received(1).NotifyAsync(
                        Arg.Is<LinkUpdate>(update =>
                            update.Id == 42
                            && update.Url == new Uri("https://github.com/user/repo")
                            && update.Description == "Repository updated"
                            && update.TgChatIds.SequenceEqual(new[] { 123L })),
                        Arg.Any<CancellationToken>());

                    return true;
                }
                catch
                {
                    return false;
                }
            });

            await deadLetterPublisher.DidNotReceive().PublishAsync(
                Arg.Any<ConsumeResult<string, byte[]>>(),
                Arg.Any<string>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ScrapperKafkaClient_WhenSendsUpdate_BotConsumerNotifiesUser()
    {
        var topic = $"link-updates-{Guid.NewGuid():N}";
        var deadLetterTopic = $"link-updates-dlq-{Guid.NewGuid():N}";

        await kafkaFixture.CreateTopicAsync(topic);
        await kafkaFixture.CreateTopicAsync(deadLetterTopic);

        var notifier = Substitute.For<ILinkUpdateNotifier>();
        var deadLetterPublisher = Substitute.For<ILinkUpdateDeadLetterPublisher>();

        using var consumer = CreateConsumer(
            topic,
            deadLetterTopic,
            notifier,
            deadLetterPublisher);

        using var scrapperKafkaClient = CreateScrapperKafkaClient(topic);

        await consumer.StartAsync(CancellationToken.None);

        try
        {
            await scrapperKafkaClient.Client.SendUpdateAsync(
                new LinkUpdate { Id = 42, Url = new Uri("https://github.com/user/repo"), Description = "Repository updated", TgChatIds = [123] });

            await WaitUntilAsync(async () =>
            {
                try
                {
                    await notifier.Received(1).NotifyAsync(
                        Arg.Is<LinkUpdate>(update =>
                            update.Id == 42
                            && update.Url == new Uri("https://github.com/user/repo")
                            && update.Description == "Repository updated"
                            && update.TgChatIds.SequenceEqual(new[] { 123L })),
                        Arg.Any<CancellationToken>());

                    return true;
                }
                catch
                {
                    return false;
                }
            });

            await deadLetterPublisher.DidNotReceive().PublishAsync(
                Arg.Any<ConsumeResult<string, byte[]>>(),
                Arg.Any<string>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Consumer_WhenMessageIsInvalid_PublishesMessageToDeadLetterTopic()
    {
        var topic = $"link-updates-{Guid.NewGuid():N}";
        var deadLetterTopic = $"link-updates-dlq-{Guid.NewGuid():N}";

        await kafkaFixture.CreateTopicAsync(topic);
        await kafkaFixture.CreateTopicAsync(deadLetterTopic);

        var notifier = Substitute.For<ILinkUpdateNotifier>();

        using var deadLetterProducer = new ProducerBuilder<Null, string>(new ProducerConfig { BootstrapServers = kafkaFixture.BootstrapServers, Acks = Acks.All, EnableIdempotence = true }).Build();

        var options = Options.Create(new LinkUpdatesKafkaOptions
        {
            BootstrapServers = kafkaFixture.BootstrapServers,
            Topic = topic,
            DeadLetterTopic = deadLetterTopic,
            GroupId = $"linktracker-bot-test-{Guid.NewGuid():N}",
            RetryAttempts = 3,
            RetryBackoffMilliseconds = 0,
            Serialization = KafkaSerializationKind.Json,
            SchemaRegistryUrl = kafkaFixture.SchemaRegistryUrl
        });

        var deadLetterPublisher = new KafkaLinkUpdateDeadLetterPublisher(
            deadLetterProducer,
            options,
            NullLogger<KafkaLinkUpdateDeadLetterPublisher>.Instance);

        using var consumer = CreateConsumer(
            topic,
            deadLetterTopic,
            notifier,
            deadLetterPublisher);

        await consumer.StartAsync(CancellationToken.None);

        try
        {
            const string invalidPayload = "{ invalid json";

            await ProduceAsync(topic, invalidPayload);

            var deadLetterPayload = await ConsumeSingleMessageAsync(deadLetterTopic);

            var deadLetterMessage = JsonSerializer.Deserialize<LinkUpdatesDeadLetterKafkaMessage>(
                deadLetterPayload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.NotNull(deadLetterMessage);

            var originalPayload = Encoding.UTF8.GetString(
                Convert.FromBase64String(deadLetterMessage.Payload));

            Assert.Equal(invalidPayload, originalPayload);
            Assert.StartsWith("Kafka сообщение не удалось десериализовать:", deadLetterMessage.Reason);
            Assert.Equal(topic, deadLetterMessage.SourceTopic);

            await notifier.DidNotReceive().NotifyAsync(
                Arg.Any<LinkUpdate>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ScrapperKafkaClient_WhenUsesAvro_BotConsumerNotifiesUser()
    {
        var topic = $"link-updates-avro-{Guid.NewGuid():N}";
        var deadLetterTopic = $"link-updates-dlq-avro-{Guid.NewGuid():N}";

        await kafkaFixture.CreateTopicAsync(topic);
        await kafkaFixture.CreateTopicAsync(deadLetterTopic);

        using var schemaRegistryClient = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = kafkaFixture.SchemaRegistryUrl });

        var notifier = Substitute.For<ILinkUpdateNotifier>();
        var deadLetterPublisher = Substitute.For<ILinkUpdateDeadLetterPublisher>();

        using var consumer = CreateAvroConsumer(
            topic,
            deadLetterTopic,
            notifier,
            deadLetterPublisher,
            schemaRegistryClient);

        using var scrapperKafkaClient = CreateAvroScrapperKafkaClient(
            topic,
            schemaRegistryClient);

        await consumer.StartAsync(CancellationToken.None);

        try
        {
            await scrapperKafkaClient.Client.SendUpdateAsync(
                new LinkUpdate { Id = 42, Url = new Uri("https://github.com/user/repo"), Description = "Repository updated with Avro", TgChatIds = [123] });

            await WaitUntilAsync(async () =>
            {
                try
                {
                    await notifier.Received(1).NotifyAsync(
                        Arg.Is<LinkUpdate>(update =>
                            update.Id == 42
                            && update.Url == new Uri("https://github.com/user/repo")
                            && update.Description == "Repository updated with Avro"
                            && update.TgChatIds.SequenceEqual(new[] { 123L })),
                        Arg.Any<CancellationToken>());

                    return true;
                }
                catch
                {
                    return false;
                }
            });

            await deadLetterPublisher.DidNotReceive().PublishAsync(
                Arg.Any<ConsumeResult<string, byte[]>>(),
                Arg.Any<string>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    private async Task ProduceAsync(string topic, string payload)
    {
        using var producer = new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = kafkaFixture.BootstrapServers, Acks = Acks.All }).Build();

        await producer.ProduceAsync(
            topic,
            new Message<string, byte[]> { Key = "test-key", Value = Encoding.UTF8.GetBytes(payload) });

        producer.Flush(TimeSpan.FromSeconds(5));
    }

    private LinkUpdatesKafkaConsumer CreateConsumer(
        string topic,
        string deadLetterTopic,
        ILinkUpdateNotifier notifier,
        ILinkUpdateDeadLetterPublisher deadLetterPublisher)
    {
        var options = Options.Create(new LinkUpdatesKafkaOptions
        {
            BootstrapServers = kafkaFixture.BootstrapServers,
            Topic = topic,
            DeadLetterTopic = deadLetterTopic,
            GroupId = $"linktracker-bot-test-{Guid.NewGuid():N}",
            RetryAttempts = 3,
            RetryBackoffMilliseconds = 0,
            Serialization = KafkaSerializationKind.Json,
            SchemaRegistryUrl = kafkaFixture.SchemaRegistryUrl
        });

        var kafkaConsumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig { BootstrapServers = kafkaFixture.BootstrapServers, GroupId = options.Value.GroupId, AutoOffsetReset = AutoOffsetReset.Earliest, EnableAutoCommit = false }).Build();

        var messageHandler = new LinkUpdatesKafkaMessageHandler(
            new JsonLinkUpdateKafkaDeserializer(),
            new KafkaLinkUpdateMessageParser(),
            deadLetterPublisher,
            notifier,
            options,
            NullLogger<LinkUpdatesKafkaMessageHandler>.Instance);

        return new LinkUpdatesKafkaConsumer(
            kafkaConsumer,
            messageHandler,
            options,
            Substitute.For<IBotMetrics>(),
            NullLogger<LinkUpdatesKafkaConsumer>.Instance);
    }

    private ScrapperKafkaClientFixture CreateScrapperKafkaClient(string topic)
    {
        var producer = new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = kafkaFixture.BootstrapServers, Acks = Acks.All, EnableIdempotence = true }).Build();

        var options = new BotKafkaOptions { BootstrapServers = kafkaFixture.BootstrapServers, Topic = topic, Serialization = KafkaSerializationKind.Json, SchemaRegistryUrl = kafkaFixture.SchemaRegistryUrl };

        var client = new BotKafkaClient(
            producer,
            new JsonLinkUpdateKafkaSerializer(),
            options,
            new ScrapperMetrics(),
            NullLogger<BotKafkaClient>.Instance);

        return new ScrapperKafkaClientFixture(client, producer);
    }

    private LinkUpdatesKafkaConsumer CreateAvroConsumer(
        string topic,
        string deadLetterTopic,
        ILinkUpdateNotifier notifier,
        ILinkUpdateDeadLetterPublisher deadLetterPublisher,
        ISchemaRegistryClient schemaRegistryClient)
    {
        var options = Options.Create(new LinkUpdatesKafkaOptions
        {
            BootstrapServers = kafkaFixture.BootstrapServers,
            Topic = topic,
            DeadLetterTopic = deadLetterTopic,
            GroupId = $"linktracker-bot-avro-test-{Guid.NewGuid():N}",
            RetryAttempts = 3,
            RetryBackoffMilliseconds = 0,
            Serialization = KafkaSerializationKind.Avro,
            SchemaRegistryUrl = kafkaFixture.SchemaRegistryUrl
        });

        var kafkaConsumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig { BootstrapServers = kafkaFixture.BootstrapServers, GroupId = options.Value.GroupId, AutoOffsetReset = AutoOffsetReset.Earliest, EnableAutoCommit = false }).Build();

        var messageHandler = new LinkUpdatesKafkaMessageHandler(
            new AvroLinkUpdateKafkaDeserializer(
                new AvroDeserializer<GenericRecord>(schemaRegistryClient)),
            new KafkaLinkUpdateMessageParser(),
            deadLetterPublisher,
            notifier,
            options,
            NullLogger<LinkUpdatesKafkaMessageHandler>.Instance);

        return new LinkUpdatesKafkaConsumer(
            kafkaConsumer,
            messageHandler,
            options,
            Substitute.For<IBotMetrics>(),
            NullLogger<LinkUpdatesKafkaConsumer>.Instance);
    }

    private ScrapperKafkaClientFixture CreateAvroScrapperKafkaClient(
        string topic,
        ISchemaRegistryClient schemaRegistryClient)
    {
        var producer = new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = kafkaFixture.BootstrapServers, Acks = Acks.All, EnableIdempotence = true }).Build();

        var options = new BotKafkaOptions { BootstrapServers = kafkaFixture.BootstrapServers, Topic = topic, Serialization = KafkaSerializationKind.Avro, SchemaRegistryUrl = kafkaFixture.SchemaRegistryUrl };

        var client = new BotKafkaClient(
            producer,
            new AvroLinkUpdateKafkaSerializer(
                new AvroSerializer<GenericRecord>(schemaRegistryClient)),
            options,
            new ScrapperMetrics(),
            NullLogger<BotKafkaClient>.Instance);

        return new ScrapperKafkaClientFixture(client, producer);
    }

    private async Task<string> ConsumeSingleMessageAsync(string topic)
    {
        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig { BootstrapServers = kafkaFixture.BootstrapServers, GroupId = $"linktracker-dlq-test-{Guid.NewGuid():N}", AutoOffsetReset = AutoOffsetReset.Earliest, EnableAutoCommit = false }).Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (!cts.IsCancellationRequested)
        {
            var result = consumer.Consume(cts.Token);

            if (result?.Message?.Value is not null)
            {
                return result.Message.Value;
            }

            await Task.Delay(100, cts.Token);
        }

        throw new TimeoutException($"Message was not consumed from topic '{topic}'.");
    }

    private static async Task WaitUntilAsync(
        Func<Task<bool>> condition,
        int timeoutMilliseconds = 10_000,
        int delayMilliseconds = 100)
    {
        using var cts = new CancellationTokenSource(timeoutMilliseconds);

        while (!cts.IsCancellationRequested)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(delayMilliseconds, cts.Token);
        }

        throw new TimeoutException("Condition was not met within timeout.");
    }

    private sealed record ScrapperKafkaClientFixture(
        BotKafkaClient Client,
        IProducer<string, byte[]> Producer) : IDisposable
    {
        public void Dispose()
        {
            Producer.Dispose();
        }
    }
}