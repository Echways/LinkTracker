using LinkTracker.AiAgent.Infrastructure.Kafka.Serialization;
using LinkTracker.Bot.Infrastructure.Kafka.Deserialization;
using LinkTracker.Shared.Contracts.AiAgent;

namespace LinkTracker.Tests.Shared.Unit.Contracts;

/// <summary>
/// AI-агент публикует ProcessedLinkUpdate, а Bot читает то же сообщение как LinkUpdate.
/// Контракты разные, связь между ними — только формат на проводе, поэтому она проверяется явно.
/// </summary>
[Trait("Module", "Shared")]
[Trait("Category", "Unit")]
public sealed class ProcessedUpdateWireCompatibilityTests
{
    private const string Topic = "link.processed-updates";

    [Theory]
    [InlineData(LinkUpdatePriority.High)]
    [InlineData(LinkUpdatePriority.Medium)]
    [InlineData(LinkUpdatePriority.Low)]
    public async Task BotDeserializer_ReadsPriorityPublishedByAiAgent(LinkUpdatePriority priority)
    {
        var published = new ProcessedLinkUpdate
        {
            Id = 42,
            Url = new Uri("https://github.com/dotnet/runtime"),
            Description = "Новый issue",
            TgChatIds = [1001],
            Priority = priority
        };

        var payload = await new JsonProcessedLinkUpdateKafkaSerializer()
            .SerializeAsync(published, Topic, CancellationToken.None);

        var received = await new JsonLinkUpdateKafkaDeserializer()
            .DeserializeAsync(payload, Topic, CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal(priority, received!.Priority);
        Assert.Equal(published.Id, received.Id);
        Assert.Equal(published.Url, received.Url);
        Assert.Equal(published.Description, received.Description);
        Assert.Equal(published.TgChatIds, received.TgChatIds);
    }
}
