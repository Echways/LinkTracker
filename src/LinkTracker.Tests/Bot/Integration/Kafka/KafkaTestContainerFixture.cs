using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Docker.DotNet;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Kafka;

namespace LinkTracker.Tests.Bot.Integration.Kafka;

public sealed class KafkaTestContainerFixture : IAsyncLifetime
{
    private const string KafkaImage = "confluentinc/cp-kafka:7.6.1";
    private const string SchemaRegistryImage = "confluentinc/cp-schema-registry:7.6.1";

    private readonly KafkaContainer _kafkaContainer;

    private readonly INetwork _network = new NetworkBuilder()
        .WithName($"linktracker-kafka-test-{Guid.NewGuid():N}")
        .Build();

    private readonly IContainer _schemaRegistryContainer;

    public KafkaTestContainerFixture()
    {
        _kafkaContainer = new KafkaBuilder(KafkaImage)
            .WithNetwork(_network)
            .WithNetworkAliases("kafka")
            .Build();

        _schemaRegistryContainer = new ContainerBuilder(SchemaRegistryImage)
            .WithNetwork(_network)
            .WithNetworkAliases("schema-registry")
            .WithPortBinding(8081, true)
            .WithEnvironment("SCHEMA_REGISTRY_HOST_NAME", "schema-registry")
            .WithEnvironment("SCHEMA_REGISTRY_LISTENERS", "http://0.0.0.0:8081")
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS", "PLAINTEXT://kafka:9093")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                request.ForPort(8081).ForPath("/subjects")))
            .Build();
    }

    public string BootstrapServers => _kafkaContainer.GetBootstrapAddress();

    public string SchemaRegistryUrl =>
        $"http://localhost:{_schemaRegistryContainer.GetMappedPublicPort(8081)}";

    public async Task InitializeAsync()
    {
        await _network.CreateAsync();

        await StartKafkaAsync();
        await _schemaRegistryContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _schemaRegistryContainer.DisposeAsync();
        await _kafkaContainer.DisposeAsync();
        await _network.DisposeAsync();
    }

    public async Task CreateTopicAsync(string topicName, int partitions = 1)
    {
        using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = BootstrapServers }).Build();

        try
        {
            await adminClient.CreateTopicsAsync(
            [
                new TopicSpecification { Name = topicName, NumPartitions = partitions, ReplicationFactor = 1 }
            ]);
        }
        catch (CreateTopicsException ex)
            when (ex.Results.Any(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
        {
        }
    }

    private async Task StartKafkaAsync()
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await _kafkaContainer.StartAsync();
                return;
            }
            catch (DockerApiException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
            }
        }

        throw new InvalidOperationException("Kafka did not start in time.");
    }
}