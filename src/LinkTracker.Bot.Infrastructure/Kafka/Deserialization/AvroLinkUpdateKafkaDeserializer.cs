using System.Collections;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry.Serdes;
using LinkTracker.Bot.Infrastructure.Kafka.Abstractions;
using LinkTracker.Shared.Contracts.AiAgent;
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
            Author = ReadOptional(record, "author") as string ?? string.Empty,
            TgChatIds = ((IEnumerable)record["tgChatIds"])
                .Cast<object>()
                .Select(Convert.ToInt64)
                .ToArray(),
            Priority = ReadEnum(record, "priority", LinkUpdatePriority.Medium),
            Kind = ReadEnum(record, "kind", LinkUpdateKind.Content)
        };
    }

    private static object? ReadOptional(GenericRecord record, string field)
    {
        return record.TryGetValue(field, out var value) ? value : null;
    }

    private static TEnum ReadEnum<TEnum>(GenericRecord record, string field, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (ReadOptional(record, field) is not GenericEnum value)
        {
            return fallback;
        }

        return Enum.TryParse<TEnum>(value.Value, out var parsed) ? parsed : fallback;
    }
}
