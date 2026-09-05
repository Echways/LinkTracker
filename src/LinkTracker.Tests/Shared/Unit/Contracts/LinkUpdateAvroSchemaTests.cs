using Avro;
using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Tests.Shared.Unit.Contracts;

[Trait("Module", "Shared")]
[Trait("Category", "Unit")]
public sealed class LinkUpdateAvroSchemaTests
{
    [Fact]
    public void Schema_CoversEveryLinkUpdateProperty()
    {
        var schema = (RecordSchema)Avro.Schema.Parse(LinkUpdateAvroSchema.Value);

        var contractFields = typeof(LinkUpdate)
            .GetProperties()
            .Select(property => char.ToLowerInvariant(property.Name[0]) + property.Name[1..])
            .Order();

        Assert.Equal(contractFields, schema.Fields.Select(field => field.Name).Order());
    }

    [Theory]
    [InlineData("author")]
    [InlineData("priority")]
    [InlineData("kind")]
    public void Schema_FieldsAddedAfterFirstVersion_HaveDefaults(string field)
    {
        var schema = (RecordSchema)Avro.Schema.Parse(LinkUpdateAvroSchema.Value);

        Assert.NotNull(schema[field].DefaultValue);
    }
}
