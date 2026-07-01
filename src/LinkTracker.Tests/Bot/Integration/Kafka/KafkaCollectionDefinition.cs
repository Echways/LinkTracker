namespace LinkTracker.Tests.Bot.Integration.Kafka;

[CollectionDefinition("Kafka collection", DisableParallelization = true)]
public sealed class KafkaCollection : ICollectionFixture<KafkaTestContainerFixture>
{
}