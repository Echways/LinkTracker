using System.Text;
using System.Text.Json;
using LinkTracker.AiAgent.Infrastructure.Kafka.Abstractions;
using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.AiAgent.Infrastructure.Kafka.Deserialization;

internal sealed class JsonRawLinkUpdateKafkaDeserializer : IRawLinkUpdateKafkaDeserializer
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public Task<LinkUpdate?> DeserializeAsync(byte[] payload, string topic, CancellationToken ct)
    {
        var json = Encoding.UTF8.GetString(payload);
        var update = JsonSerializer.Deserialize<LinkUpdate>(json, JsonSerializerOptions);
        return Task.FromResult(update);
    }
}