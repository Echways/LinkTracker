using System.Text.Json;
using LinkTracker.Scrapper.Infrastructure.Outbox.Abstractions;
using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Scrapper.Infrastructure.Outbox.Serialization;

internal sealed class SystemTextJsonOutboxMessageSerializer : IOutboxMessageSerializer
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public string Serialize(LinkUpdate update)
    {
        return JsonSerializer.Serialize(update, JsonSerializerOptions);
    }

    public LinkUpdate? Deserialize(string payload)
    {
        return JsonSerializer.Deserialize<LinkUpdate>(payload, JsonSerializerOptions);
    }
}