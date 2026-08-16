using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Application.Services;
using LinkTracker.AiAgent.Application.Telemetry.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Clients.Kafka;
using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.AiAgent.Infrastructure.Configuration.Kafka;
using LinkTracker.AiAgent.Infrastructure.Kafka.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Kafka.Deserialization;
using LinkTracker.AiAgent.Infrastructure.Services;
using LinkTracker.Shared.Contracts.AiAgent;
using LinkTracker.Shared.Contracts.Bot;
using LinkTracker.Tests.Bot.Integration.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LinkTracker.Tests.AiAgent.Integration.Infrastructure.Clients.Kafka;

[Collection("AiAgent Kafka collection")]
[Trait("Module", "AiAgent")]
[Trait("Category", "Integration")]
public sealed class RawUpdatesKafkaConsumerIntegrationTests(KafkaTestContainerFixture kafkaFixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Consumer_WhenValidMessagePublished_AddsUpdateToGroupingBuffer()
    {
        var topic = $"link-raw-{Guid.NewGuid():N}";
        await kafkaFixture.CreateTopicAsync(topic);

        var groupingBuffer = Substitute.For<IGroupingBuffer>();
        using var consumer = BuildConsumer(topic, groupingBuffer);

        await consumer.StartAsync(CancellationToken.None);

        try
        {
            await ProduceAsync(topic, new LinkUpdate
            {
                Id = 100,
                Url = new Uri("https://github.com/user/repo"),
                Description = "A long enough description that passes the min-length filter",
                Author = "regular-user",
                TgChatIds = [42]
            });

            await WaitUntilAsync(() =>
            {
                try
                {
                    groupingBuffer.Received(1).Add(
                        42L,
                        Arg.Is<ProcessedLinkUpdate>(u => u.Id == 100),
                        Arg.Any<IMessageAck>());

                    return Task.FromResult(true);
                }
                catch
                {
                    return Task.FromResult(false);
                }
            });
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Consumer_WhenFilteredMessagePublished_DoesNotAddToGroupingBuffer()
    {
        var topic = $"link-raw-filtered-{Guid.NewGuid():N}";
        await kafkaFixture.CreateTopicAsync(topic);

        var groupingBuffer = Substitute.For<IGroupingBuffer>();
        using var consumer = BuildConsumer(topic, groupingBuffer);

        await consumer.StartAsync(CancellationToken.None);

        try
        {
            await ProduceAsync(topic, new LinkUpdate
            {
                Id = 200,
                Url = new Uri("https://github.com/user/repo"),
                Description = "short",
                Author = "regular-user",
                TgChatIds = [42]
            });

            await Task.Delay(TimeSpan.FromSeconds(3));

            groupingBuffer.DidNotReceive().Add(Arg.Any<long>(), Arg.Any<ProcessedLinkUpdate>(), Arg.Any<IMessageAck>());
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Consumer_WhenMalformedMessagePublished_DoesNotCrash()
    {
        var topic = $"link-raw-bad-{Guid.NewGuid():N}";
        await kafkaFixture.CreateTopicAsync(topic);

        var groupingBuffer = Substitute.For<IGroupingBuffer>();
        using var consumer = BuildConsumer(topic, groupingBuffer);

        await consumer.StartAsync(CancellationToken.None);

        try
        {
            await ProduceRawAsync(topic, "{ this is not valid json !!!");

            await Task.Delay(TimeSpan.FromSeconds(3));

            groupingBuffer.DidNotReceive().Add(Arg.Any<long>(), Arg.Any<ProcessedLinkUpdate>(), Arg.Any<IMessageAck>());
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Consumer_WhenMalformedMessagePublished_PublishesMessageToDeadLetterTopic()
    {
        var topic = $"link-raw-dlq-{Guid.NewGuid():N}";
        var deadLetterTopic = $"link-raw-dlq-{Guid.NewGuid():N}-dlq";

        await kafkaFixture.CreateTopicAsync(topic);
        await kafkaFixture.CreateTopicAsync(deadLetterTopic);

        using var deadLetterProducer = new ProducerBuilder<string, byte[]>(
            new ProducerConfig { BootstrapServers = kafkaFixture.BootstrapServers, Acks = Acks.All, EnableIdempotence = true }).Build();

        var deadLetterPublisher = new RawUpdatesKafkaDeadLetterPublisher(
            deadLetterProducer,
            Options.Create(new RawUpdatesKafkaOptions { DeadLetterTopic = deadLetterTopic }),
            NullLogger<RawUpdatesKafkaDeadLetterPublisher>.Instance);

        var groupingBuffer = Substitute.For<IGroupingBuffer>();
        using var consumer = BuildConsumer(topic, groupingBuffer, deadLetterPublisher, deadLetterTopic);

        await consumer.StartAsync(CancellationToken.None);

        try
        {
            await ProduceRawAsync(topic, "{ this is not valid json !!!");

            var deadLettered = ReadFirstMessage(deadLetterTopic);

            Assert.NotNull(deadLettered);

            using var document = JsonDocument.Parse(deadLettered);
            var root = document.RootElement;

            Assert.Equal(topic, root.GetProperty("sourceTopic").GetString());
            Assert.Contains("десериализовать", root.GetProperty("reason").GetString());
            Assert.Equal(
                "{ this is not valid json !!!",
                Encoding.UTF8.GetString(Convert.FromBase64String(root.GetProperty("payload").GetString()!)));

            groupingBuffer.DidNotReceive().Add(Arg.Any<long>(), Arg.Any<ProcessedLinkUpdate>(), Arg.Any<IMessageAck>());
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    private string? ReadFirstMessage(string topic)
    {
        using var consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = kafkaFixture.BootstrapServers,
            GroupId = $"dlq-reader-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();

        consumer.Subscribe(topic);

        try
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(30));

            return result is null ? null : Encoding.UTF8.GetString(result.Message.Value);
        }
        finally
        {
            consumer.Close();
        }
    }

    private RawUpdatesKafkaConsumer BuildConsumer(
        string topic,
        IGroupingBuffer groupingBuffer,
        IRawUpdateDeadLetterPublisher? deadLetterPublisher = null,
        string deadLetterTopic = "unused-dlq")
    {
        var summarizer = Substitute.For<ILinkUpdateSummarizer>();
        summarizer.SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromResult((string)x[0]));

        var prioritizer = Substitute.For<ILinkUpdatePrioritizer>();
        prioritizer.Prioritize(Arg.Any<string>()).Returns(LinkUpdatePriority.Medium);

        var consumerOpts = Options.Create(new RawUpdatesKafkaOptions
        {
            BootstrapServers = kafkaFixture.BootstrapServers,
            Topic = topic,
            GroupId = $"test-group-{Guid.NewGuid():N}",
            DeadLetterTopic = deadLetterTopic,
            RetryAttempts = 1,
            RetryBackoffMilliseconds = 0
        });

        var aiAgentOpts = Options.Create(new AiAgentOptions { Filtering = new FilteringOptions { MinLength = 10, StopWords = [], ExcludedAuthors = [] }, Summarization = new SummarizationOptions { Threshold = 1000 } });

        var processingService = new LinkUpdateProcessingService(
            new LinkUpdateFilter(aiAgentOpts),
            summarizer,
            prioritizer,
            groupingBuffer,
            Substitute.For<IProcessedUpdatePublisher>(),
            NullLogger<LinkUpdateProcessingService>.Instance);

        var messageHandler = new RawUpdatesKafkaMessageHandler(
            new JsonRawLinkUpdateKafkaDeserializer(),
            processingService,
            deadLetterPublisher ?? Substitute.For<IRawUpdateDeadLetterPublisher>(),
            consumerOpts,
            Substitute.For<IAiAgentMetrics>(),
            NullLogger<RawUpdatesKafkaMessageHandler>.Instance);

        var kafkaConsumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = kafkaFixture.BootstrapServers,
            GroupId = consumerOpts.Value.GroupId,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = false
        }).Build();

        return new RawUpdatesKafkaConsumer(
            kafkaConsumer, messageHandler, new KafkaOffsetTracker(), consumerOpts,
            Substitute.For<IAiAgentMetrics>(),
            NullLogger<RawUpdatesKafkaConsumer>.Instance);
    }

    private async Task ProduceAsync(string topic, LinkUpdate update)
    {
        await ProduceRawAsync(topic, JsonSerializer.Serialize(update, JsonOptions));
    }

    private async Task ProduceRawAsync(string topic, string payload)
    {
        using var producer = new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = kafkaFixture.BootstrapServers }).Build();

        await producer.ProduceAsync(topic, new Message<string, byte[]> { Key = Guid.NewGuid().ToString(), Value = Encoding.UTF8.GetBytes(payload) });
    }

    private static async Task WaitUntilAsync(
        Func<Task<bool>> condition,
        TimeSpan? timeout = null,
        TimeSpan? interval = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        var delay = interval ?? TimeSpan.FromMilliseconds(300);

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(delay);
        }

        throw new TimeoutException("Condition was not met within timeout.");
    }
}