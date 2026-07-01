namespace LinkTracker.Shared.Contracts.Bot;

public static class LinkUpdateAvroSchema
{
    public const string Value =
        """
        {
          "type": "record",
          "name": "LinkUpdate",
          "namespace": "LinkTracker.Shared.Contracts.Bot",
          "fields": [
            { "name": "id", "type": "long" },
            { "name": "url", "type": "string" },
            { "name": "description", "type": "string" },
            {
              "name": "tgChatIds",
              "type": {
                "type": "array",
                "items": "long"
              }
            }
          ]
        }
        """;
}