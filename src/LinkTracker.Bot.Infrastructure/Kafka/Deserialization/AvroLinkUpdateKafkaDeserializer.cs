using System.Collections;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry.Serdes;
using LinkTracker.Bot.Infrastructure.Kafka.Abstractions;
using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Bot.Infrastructure.Kafka.Deserialization;

internal sealed class AvroLinkUpdateKafkaDeserializer(
    AvroDeserializer<GenericRecord> deserializer) : ILinkUpdateKafkaDeserializer
{
    public async Task<LinkUpdate?> DeserializeAsync(byte[] payload, string topic, CancellationToken ct)
    {
        var record = await deserializer.DeserializeAsync(
            payload,
            false,
            new SerializationContext(MessageComponentType.Value, topic));

        return new LinkUpdate
        {
            Id = Convert.ToInt64(record["id"]),
            Url = new Uri((string)record["url"]),
            Description = (string)record["description"],
            TgChatIds = ((IEnumerable)record["tgChatIds"])
                .Cast<object>()
                .Select(Convert.ToInt64)
                .ToArray()
        };
    }
}