using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using LinkTracker.AiAgent.Infrastructure.Clients.Kafka;
using LinkTracker.AiAgent.Infrastructure.Configuration.Kafka;
using LinkTracker.AiAgent.Infrastructure.Kafka.Serialization;
using LinkTracker.Shared.Contracts.AiAgent;
using LinkTracker.Tests.Bot.Integration.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkTracker.Tests.AiAgent.Integration.Infrastructure.Clients.Kafka;

[Collection("AiAgent Kafka collection")]
[Trait("Module", "AiAgent")]
[Trait("Category", "Integration")]
public sealed class ProcessedUpdatesKafkaPublisherIntegrationTests(KafkaTestContainerFixture kafkaFixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PublishAsync_WhenUpdateProcessed_MessageAppearsInOutputTopic()
    {
        var topic = $"link-processed-{Guid.NewGuid():N}";
        await kafkaFixture.CreateTopicAsync(topic);

        var publisher = BuildPublisher(topic);

        var update = new ProcessedLinkUpdate
        {
            Id = 300,
            Url = new Uri("https://github.com/user/repo"),
            Description = "critical security fix applied",
            TgChatIds = [42],
            Priority = LinkUpdatePriority.High
        };

        await publisher.PublishAsync(update, CancellationToken.None);

        var consumed = await ConsumeOneAsync(topic);

        Assert.NotNull(consumed);
        Assert.Equal(300, consumed.Id);
        Assert.Equal(LinkUpdatePriority.High, consumed.Priority);
        Assert.Equal("critical security fix applied", consumed.Description);
    }

    [Fact]
    public async Task PublishAsync_WhenFilteredUpdate_NothingAppearsInOutputTopic()
    {
        var topic = $"link-processed-empty-{Guid.NewGuid():N}";
        await kafkaFixture.CreateTopicAsync(topic);

        var consumed = await ConsumeOneAsync(topic, TimeSpan.FromSeconds(3));

        Assert.Null(consumed);
    }

    private ProcessedUpdatesKafkaPublisher BuildPublisher(string topic)
    {
        var opts = Options.Create(new ProcessedUpdatesKafkaOptions { BootstrapServers = kafkaFixture.BootstrapServers, Topic = topic });

        var producer = new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = kafkaFixture.BootstrapServers }).Build();

        return new ProcessedUpdatesKafkaPublisher(
            producer,
            new JsonProcessedLinkUpdateKafkaSerializer(),
            opts,
            NullLogger<ProcessedUpdatesKafkaPublisher>.Instance);
    }

    private Task<ProcessedLinkUpdate?> ConsumeOneAsync(string topic, TimeSpan? timeout = null)
    {
        try
        {
            var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

            using var consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig { BootstrapServers = kafkaFixture.BootstrapServers, GroupId = $"test-reader-{Guid.NewGuid():N}", AutoOffsetReset = AutoOffsetReset.Earliest, EnableAutoCommit = false }).Build();

            consumer.Subscribe(topic);

            try
            {
                while (DateTimeOffset.UtcNow < deadline)
                {
                    var result = consumer.Consume(TimeSpan.FromMilliseconds(500));

                    if (result is null)
                    {
                        continue;
                    }

                    var json = Encoding.UTF8.GetString(result.Message.Value);
                    return Task.FromResult(JsonSerializer.Deserialize<ProcessedLinkUpdate>(json, JsonOptions));
                }
            }
            finally
            {
                consumer.Close();
            }

            return Task.FromResult<ProcessedLinkUpdate?>(null);
        }
        catch (Exception exception)
        {
            return Task.FromException<ProcessedLinkUpdate?>(exception);
        }
    }
}