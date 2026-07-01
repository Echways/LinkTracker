using System.Text;
using System.Text.Json;
using LinkTracker.AiAgent.Infrastructure.Kafka.Abstractions;
using LinkTracker.Shared.Contracts.AiAgent;

namespace LinkTracker.AiAgent.Infrastructure.Kafka.Serialization;

internal sealed class JsonProcessedLinkUpdateKafkaSerializer : IProcessedLinkUpdateKafkaSerializer
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public Task<byte[]> SerializeAsync(ProcessedLinkUpdate update, string topic, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(update, JsonSerializerOptions);
        return Task.FromResult(Encoding.UTF8.GetBytes(json));
    }
}