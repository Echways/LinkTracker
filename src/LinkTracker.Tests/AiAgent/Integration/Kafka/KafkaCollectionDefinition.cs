using LinkTracker.Tests.Bot.Integration.Kafka;

namespace LinkTracker.Tests.AiAgent.Integration.Kafka;

[CollectionDefinition("AiAgent Kafka collection")]
public sealed class AiAgentKafkaCollectionDefinition : ICollectionFixture<KafkaTestContainerFixture>;