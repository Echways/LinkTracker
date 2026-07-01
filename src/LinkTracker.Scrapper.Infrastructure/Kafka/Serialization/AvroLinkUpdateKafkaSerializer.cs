using Avro;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry.Serdes;
using LinkTracker.Scrapper.Infrastructure.Kafka.Abstractions;
using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Scrapper.Infrastructure.Kafka.Serialization;

internal sealed class AvroLinkUpdateKafkaSerializer(
    AvroSerializer<GenericRecord> serializer) : ILinkUpdateKafkaSerializer
{
    private static readonly RecordSchema Schema =
        (RecordSchema)Avro.Schema.Parse(LinkUpdateAvroSchema.Value);

    public Task<byte[]> SerializeAsync(LinkUpdate update, string topic, CancellationToken ct)
    {
        var record = new GenericRecord(Schema);
        record.Add("id", update.Id);
        record.Add("url", update.Url.ToString());
        record.Add("description", update.Description);
        record.Add("tgChatIds", update.TgChatIds.ToArray());

        return serializer.SerializeAsync(
            record,
            new SerializationContext(MessageComponentType.Value, topic));
    }
}