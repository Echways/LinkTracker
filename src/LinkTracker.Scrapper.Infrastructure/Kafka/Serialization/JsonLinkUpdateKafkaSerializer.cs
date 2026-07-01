using System.Text;
using System.Text.Json;
using LinkTracker.Scrapper.Infrastructure.Kafka.Abstractions;
using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Scrapper.Infrastructure.Kafka.Serialization;

internal sealed class JsonLinkUpdateKafkaSerializer : ILinkUpdateKafkaSerializer
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public Task<byte[]> SerializeAsync(LinkUpdate update, string topic, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(update, JsonSerializerOptions);
        return Task.FromResult(Encoding.UTF8.GetBytes(json));
    }
}